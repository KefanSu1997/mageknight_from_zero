"""Tiny PPO over actual Unity controls. No pixels, simulated rules or teacher actions.

Unity exports active controls and executes EventSystem pointer events. Every
transition used by PPO is a real Demo click. Setup and validation scripts are
stored separately and never counted as policy samples.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import time
import uuid
import numpy as np
from local_unity_mcp import UnityMcp

ROOT = Path(__file__).resolve().parents[2]
PHASES = ('None', 'Ranged', 'Block', 'AssignDamage', 'Melee', 'End')
NUMERIC_KEYS = ('hand', 'played', 'melee', 'ranged', 'siege', 'block', 'wounds',
                'kills', 'red', 'blue', 'white', 'green', 'rejections', 'attackSlot')
TEXT_KEYS = ('ui.cardMode', 'ui.Heading', 'ui.Details', 'ui.TargetPreview', 'ui.Feedback')
SCOPE = ('Btn_Play', 'Btn_Sideways', 'Btn_Next', 'Btn_Resolve', 'Btn_AttackParts',
         'Btn_BlockParts', 'Btn_HandPage', 'Btn_MoreChoices', 'Btn_ManaPayment',
         'Btn_Done', 'Btn_All', 'Btn_Clear', 'Btn_Cancel', 'Btn_Previous', 'Btn_ExtraDie')
PREFIXES = ('Btn_Card_', 'Btn_Choice_', 'Btn_Source_', 'Btn_Die_', 'Btn_Required_',
            'Btn_Material_', 'Btn_Part_')
CATEGORIES = ('Card', 'Choice', 'Play', 'Sideways', 'Next', 'Target', 'Resolve',
              'AttackParts', 'BlockParts', 'ManaPayment', 'Source', 'Die', 'Material', 'Done', 'All', 'Other')
SCHEMA = {'version': 1, 'global': 96, 'candidate': 64, 'hidden': 64,
          'numeric_keys': NUMERIC_KEYS, 'text_keys': TEXT_KEYS,
          'scope_exact': SCOPE, 'scope_prefixes': PREFIXES,
          'mask': 'active interactable in-screen center raycast hits exact button; no rule oracle',
          'history': 'feedforward pilot; no recurrent memory',
          'scope': 'initial battle controls only; reset/configuration excluded from policy'}


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    for attempt in range(40):
        try:
            os.replace(temporary, path)
            break
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.025)


def state(observation):
    rows = observation['state']
    result = {row['key']: row['value'] for row in rows}
    if len(result) != len(rows):
        raise ValueError('Duplicate state keys')
    return result


def category(name):
    for index, kind in enumerate(CATEGORIES[:-1]):
        if name == 'Btn_' + kind or name.startswith('Btn_' + kind + '_'):
            return index
    return len(CATEGORIES) - 1


def candidates(observation):
    controls = observation['controls']
    if len({c['path'] for c in controls}) != len(controls):
        raise ValueError('Ambiguous control paths')
    return [c for c in controls if c['clickable'] and c['interactable'] and
            (c['name'] in SCOPE or c['name'].startswith(PREFIXES) or
             (c['name'] == 'Btn_Target' and '/Enemy_' in c['path']))]


def hashed_text(text, dimensions):
    features = np.zeros(dimensions, dtype=np.float64)
    for char in text[:512]:
        digest = hashlib.blake2s(char.encode('utf-8'), digest_size=4).digest()
        number = int.from_bytes(digest, 'little')
        features[number % dimensions] += 1 if number & 0x80000000 else -1
    return features / max(1, np.linalg.norm(features))


def features(observation):
    public = state(observation)
    controls = candidates(observation)
    local = np.zeros((len(controls), 64))
    for index, control in enumerate(controls):
        local[index, category(control['name'])] = 1
        local[index, 16:22] = (control['x'] / observation['screenWidth'],
            control['y'] / observation['screenHeight'], control['width'] / observation['screenWidth'],
            control['height'] / observation['screenHeight'], float('●' in control['text']),
            float('已选' in control['text']))
        local[index, 32:] = hashed_text(control['name'] + ' ' + control['text'], 32)
    global_input = np.zeros(96)
    if public.get('phase') in PHASES:
        global_input[PHASES.index(public['phase'])] = 1
    for index, key in enumerate(NUMERIC_KEYS):
        try:
            global_input[6 + index] = np.tanh(float(public.get(key, '0')) / 10)
        except ValueError:
            raise ValueError('Invalid numeric public field: ' + key)
    global_input[20:32] = hashed_text(' '.join(public.get(k, '') for k in TEXT_KEYS), 12)
    if controls:
        global_input[32:] = local.mean(axis=0)
    # rl.sharedRuleState, deck order and audit-only metadata never enter these features.
    return global_input, local, controls


class TinyPpo:
    def __init__(self, seed=37):
        self.rng = np.random.default_rng(seed)
        self.parameters = {
            'aw': self.rng.normal(0, .08, (160, 64)), 'ab': np.zeros(64),
            'ao': self.rng.normal(0, .01, 64),
            'vw': self.rng.normal(0, .08, (96, 64)), 'vb': np.zeros(64),
            'vo': self.rng.normal(0, .01, 64), 'vob': np.zeros(1)}
        self.m = {k: np.zeros_like(v) for k, v in self.parameters.items()}
        self.v = {k: np.zeros_like(v) for k, v in self.parameters.items()}
        self.updates = 0
        self.adam_steps = 0

    def forward(self, global_input, local):
        p = self.parameters
        joint = np.concatenate((np.broadcast_to(global_input, (len(local), 96)), local), axis=1)
        hidden = np.tanh(joint @ p['aw'] + p['ab'])
        logits = hidden @ p['ao']
        probs = np.exp(logits - logits.max()) if len(logits) else np.zeros(0)
        if len(probs):
            probs /= probs.sum()
        vh = np.tanh(global_input @ p['vw'] + p['vb'])
        value = float(vh @ p['vo'] + p['vob'][0])
        return probs, value, (joint, hidden, vh)

    def loss_gradient(self, samples, advantages, returns):
        p = self.parameters
        gradients = {k: np.zeros_like(v) for k, v in p.items()}
        loss = 0.
        for sample, advantage, target in zip(samples, advantages, returns):
            probs, value, (joint, hidden, vh) = self.forward(sample['g'], sample['c'])
            action = sample['action']
            logprob = np.log(probs[action])
            ratio = np.exp(logprob - sample['old_logprob'])
            clipped = np.clip(ratio, .8, 1.2)
            entropy = -float(probs @ np.log(probs))
            loss += -min(ratio * advantage, clipped * advantage) + .5 * (value-target)**2 - .01*entropy
            active = not ((advantage >= 0 and ratio > 1.2) or (advantage < 0 and ratio < .8))
            derivative = -advantage * ratio if active else 0.
            dlogits = -derivative * probs
            dlogits[action] += derivative
            dlogits += .01 * probs * (np.log(probs) + entropy)
            gradients['ao'] += hidden.T @ dlogits
            dhidden = dlogits[:, None] * p['ao'] * (1-hidden**2)
            gradients['aw'] += joint.T @ dhidden
            gradients['ab'] += dhidden.sum(axis=0)
            dv = value-target
            gradients['vo'] += vh * dv
            gradients['vob'][0] += dv
            dvhidden = dv * p['vo'] * (1-vh**2)
            gradients['vw'] += np.outer(sample['g'], dvhidden)
            gradients['vb'] += dvhidden
        count = len(samples)
        return loss/count, {k: v/count for k, v in gradients.items()}

    def update(self, samples, bootstrap):
        advantages = np.zeros(len(samples))
        next_value, tail = bootstrap, 0.
        for index in reversed(range(len(samples))):
            sample = samples[index]
            alive = 1-float(sample['done'])
            delta = sample['reward'] + .99*next_value*alive - sample['value']
            tail = delta + .99*.95*alive*tail
            advantages[index] = tail
            next_value = sample['value']
        returns = advantages + np.array([s['value'] for s in samples])
        normalized = (advantages-advantages.mean()) / max(advantages.std(), 1e-8)
        before = {k: v.copy() for k, v in self.parameters.items()}
        losses = []
        for _ in range(4):
            loss, gradients = self.loss_gradient(samples, normalized, returns)
            norm = np.sqrt(sum(float((g*g).sum()) for g in gradients.values()))
            self.adam_steps += 1
            for key, gradient in gradients.items():
                gradient = gradient * min(1, .5/max(norm, 1e-12))
                self.m[key] = .9*self.m[key] + .1*gradient
                self.v[key] = .999*self.v[key] + .001*gradient**2
                self.parameters[key] -= .001 * (self.m[key]/(1-.9**self.adam_steps)) / (
                    np.sqrt(self.v[key]/(1-.999**self.adam_steps)) + 1e-8)
            losses.append(loss)
        self.updates += 1
        for values in self.parameters.values():
            if not np.isfinite(values).all():
                raise ValueError('Nonfinite PPO parameters')
        return {'update': self.updates, 'samples': len(samples), 'losses': losses,
                'changed_arrays': sum(not np.array_equal(before[k], v) for k, v in self.parameters.items()),
                'max_parameter_change': max(float(np.max(np.abs(before[k]-v))) for k, v in self.parameters.items())}

    def save(self, directory):
        directory = Path(directory)
        directory.mkdir(parents=True, exist_ok=True)
        np.savez(directory/'model.npz', **self.parameters,
                 **{'m_'+k: v for k, v in self.m.items()}, **{'v_'+k: v for k, v in self.v.items()})
        source_paths = [Path(__file__), ROOT/'Assets/Scripts/SceneAutomation/StructuredUiPilot.cs',
                        ROOT/'Assets/Scripts/SceneAutomation/Editor/StructuredUiPilotMenu.cs']
        write_json(directory/'metadata.json', {'schema': SCHEMA, 'rng': self.rng.bit_generator.state,
            'updates': self.updates, 'adam_steps': self.adam_steps,
            'parameter_count': sum(v.size for v in self.parameters.values()), 'backend': 'NumPy CPU float64',
            'source_sha256': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_paths}})

    @classmethod
    def load(cls, directory):
        metadata = json.loads((Path(directory)/'metadata.json').read_text(encoding='utf-8'))
        if json.loads(json.dumps(SCHEMA)) != metadata['schema']:
            raise ValueError('Checkpoint feature contract mismatch')
        model = cls()
        arrays = np.load(Path(directory)/'model.npz', allow_pickle=False)
        for k in model.parameters:
            model.parameters[k] = arrays[k].copy()
            model.m[k], model.v[k] = arrays['m_'+k].copy(), arrays['v_'+k].copy()
        model.rng.bit_generator.state = metadata['rng']
        model.updates, model.adam_steps = metadata['updates'], metadata['adam_steps']
        return model


class Demo:
    def __init__(self, directory):
        self.directory = Path(directory).resolve()
        self.directory.mkdir(parents=True, exist_ok=False)
        self.calls = 0
        (ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').write_text(str(self.directory), encoding='utf-8')
        result = UnityMcp().call('execute_menu_item', {'menu_path': 'Tools/Mage Knight/Battle/Start Structured UI Pilot'})
        write_json(self.directory/'launch.json', result)
        content = result.get('result', {}).get('structuredContent', {})
        if not content.get('success'):
            raise RuntimeError('Unity pilot menu failed: ' + str(result))
        self.initial_observation = self.call('observe', timeout=90)['observation']

    def original_opening(self):
        observation = self.initial_observation
        steps = []
        # Deterministic fixture admission through actual UI, never PPO samples.
        opening = ('Btn_Hero_Tovak', 'Btn_BattleHero', 'Btn_Tactic_day_1',
                   'Btn_BeginTurn', 'Btn_JourneyEncounter_0')
        for name in opening:
            matches = [c for c in observation['controls'] if c['name'] == name and c['clickable']]
            if len(matches) != 1:
                raise ValueError('Original opening control missing or ambiguous: ' + name)
            result = self.call('click', observation, matches[0], screenshot=name=='Btn_JourneyEncounter_0')
            steps.append({'role': 'scripted_setup_not_policy', 'name': name, 'actual': result})
            observation = result['observation']
            write_json(self.directory/'setup.json', {'steps': steps})
        actual = state(observation)
        expected = {'hero.id': 'Tovak', 'hero.level': '1', 'hero.armor': '2', 'hand': '5',
                    'deck.count': '11', 'phase': 'Ranged', 'units.count': '0'}
        if any(actual.get(k) != v for k, v in expected.items()):
            raise ValueError('Original opening actual state differs: ' + str({k: actual.get(k) for k in expected}))
        write_json(self.directory/'setup_validation.json', {'status': 'passed', 'expected': expected,
            'actual': {k: actual[k] for k in expected}, 'scope': 'original battle demo; not full map standard-start acceptance'})
        return observation

    def call(self, operation, observation=None, control=None, screenshot=False, timeout=30):
        self.calls += 1
        request_id = f'{self.calls:04d}_{uuid.uuid4().hex[:8]}'
        request = {'id': request_id, 'operation': operation, 'screenshot': screenshot}
        if control:
            request.update(path=control['path'], expectedVersion=observation['version'])
        write_json(self.directory/'request.json', request)
        started = time.monotonic()
        while time.monotonic()-started < timeout:
            path = self.directory/'responses'/f'{request_id}.json'
            if path.exists():
                try:
                    result = json.loads(path.read_text(encoding='utf-8'))
                except (OSError, json.JSONDecodeError):
                    time.sleep(.05)
                    continue
                if result['id'] == request_id:
                    write_json(self.directory/'exchanges'/f'{request_id}.json', {'request': request, 'response': result})
                    if not result['success']:
                        raise RuntimeError('Actual UI exchange failed: ' + str(result.get('error')))
                    return result
            time.sleep(.05)
        write_json(self.directory/'timeout.json', {'request': request, 'elapsed': time.monotonic()-started})
        raise TimeoutError('Unity UI response timeout; do not fabricate a transition')

    def close(self):
        self.call('stop')
        # Allow the explicit exit to finish before another scene launch.
        time.sleep(1)


def reward(before, after):
    b, a = state(before), state(after)
    rejection = int(a.get('rejections', 0))-int(b.get('rejections', 0))
    played = max(0, int(a.get('played', 0))-int(b.get('played', 0)))
    kills = max(0, int(a.get('kills', 0))-int(b.get('kills', 0)))
    phase_changed = a.get('phase') != b.get('phase')
    # Pilot navigation/progress proxy only; separate from raw game rules and win rate.
    return -.02 - .2*rejection + .5*played + kills + .1*phase_changed


def rollout(model, directory, horizon, frozen=False):
    demo = Demo(directory)
    samples, trace = [], []
    try:
        observation = demo.original_opening()
        for index in range(horizon):
            g, c, controls = features(observation)
            if not controls or state(observation).get('phase') == 'End':
                break
            probs, value, _ = model.forward(g, c)
            action = int(model.rng.choice(len(controls), p=probs))
            result = demo.call('click', observation, controls[action], screenshot=index % 5 == 0)
            after = result['observation']
            score = reward(observation, after)
            done = state(after).get('phase') == 'End'
            samples.append({'g': g, 'c': c, 'action': action, 'old_logprob': float(np.log(probs[action])),
                'value': value, 'reward': score, 'done': done})
            trace.append({'index': index, 'role': 'frozen_policy' if frozen else 'training_policy',
                'request_id': result['id'], 'before_version': observation['version'],
                'candidate_paths': [control['path'] for control in controls],
                'probabilities': probs.tolist(), 'selected': controls[action], 'action_index': action,
                'value': value, 'reward': score, 'done': done, 'actual': result})
            write_json(Path(directory)/'trajectory.json', {'schema': SCHEMA, 'steps': trace})
            observation = after
            if done:
                break
        g, c, _ = features(observation)
        bootstrap = 0 if samples and samples[-1]['done'] else model.forward(g, c)[1]
        write_json(Path(directory)/'summary.json', {'steps': len(samples), 'phase': state(observation).get('phase'),
            'won': state(observation).get('won'), 'return': sum(s['reward'] for s in samples),
            'terminated': bool(samples and samples[-1]['done']), 'truncated': bool(samples and not samples[-1]['done']),
            'bootstrap': bootstrap, 'scope': 'real UI pilot, not full original gameplay or human usability acceptance'})
        return samples, bootstrap
    finally:
        demo.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    parser.add_argument('--updates', type=int, default=2)
    parser.add_argument('--horizon', type=int, default=32)
    parser.add_argument('--checkpoint')
    parser.add_argument('--frozen', action='store_true')
    args = parser.parse_args()
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    model = TinyPpo.load(args.checkpoint) if args.checkpoint else TinyPpo()
    model.save(out/'initial')
    for update in range(args.updates):
        samples, bootstrap = rollout(model, out/f'episode_{update:03d}', args.horizon, args.frozen)
        if not samples:
            raise RuntimeError('No actual UI samples collected')
        result = {'samples': len(samples), 'frozen': True} if args.frozen else model.update(samples, bootstrap)
        model.save(out/f'checkpoint_{update+1:03d}')
        write_json(out/f'update_{update+1:03d}.json', result)
        print(json.dumps(result), flush=True)


if __name__ == '__main__':
    main()
