"""Frozen pretrained text reader feeding the existing real-control PPO head."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import numpy as np
from structured_ui_ppo import TinyPpo, Demo, features, state, reward, write_json, ROOT, SCHEMA, category, hashed_text
from frozen_ui_text import FrozenTextEncoder, PublicUiTexts, WIDTH
from compare_unity_state_report import compare_report

GLOBAL = 96+WIDTH
LOCAL = 64+WIDTH


class FrozenTextFeatures:
    def __init__(self, encoder, deed_inputs=False):
        self.encoder = encoder
        self.public = PublicUiTexts()
        self.contract = {'schema': 'frozen-pretrained-ui-text@1', 'legacy': SCHEMA,
            'global': GLOBAL, 'candidate': LOCAL, 'encoder': encoder.signature,
            'original_description_sha256': self.public.signature,
            'public_state_keys': ['phase', *(__import__('structured_ui_ppo').NUMERIC_KEYS),
                                 *(__import__('structured_ui_ppo').TEXT_KEYS)],
            'text_source': 'exact original card faces plus actual UI text; no hidden audit/teacher',
            'scope': 'same initial Tovak combat UI scope; not Laya or official Jev/RLCD reproduction'}
        self.deed_inputs = deed_inputs
        if deed_inputs:
            self.contract['extra_ui_scope'] = {'parent': '/DeedInputs/',
                'exact': ['Btn_CloseInputs', 'Btn_ConfirmInputs'], 'prefix': 'Btn_InputChoice_'}

    def __call__(self, observation):
        g, c, controls = features(observation)
        if self.deed_inputs:
            extras = [control for control in observation['controls'] if '/DeedInputs/' in control['path']
                and control['clickable'] and control['interactable'] and (control['name'] in
                ('Btn_CloseInputs', 'Btn_ConfirmInputs') or control['name'].startswith('Btn_InputChoice_'))]
            for control in extras:
                row = np.zeros(64)
                row[category(control['name'])] = 1
                row[16:22] = (control['x']/observation['screenWidth'], control['y']/observation['screenHeight'],
                    control['width']/observation['screenWidth'], control['height']/observation['screenHeight'],
                    float('●' in control['text']), float('已选' in control['text']))
                row[32:] = hashed_text(control['name']+' '+control['text'], 32)
                c = np.concatenate((c, row[None]))
                controls.append(control)
            if len(c):
                g[32:] = c.mean(axis=0)
        state_text = self.public.state_text(observation, controls)
        descriptions = [self.public.control_text(control, observation) for control in controls]
        vectors = self.encoder.encode([state_text, *descriptions])
        return np.concatenate((g, vectors[0])), np.concatenate((c, vectors[1:]), axis=1), controls, {
            'state_text': state_text, 'candidate_texts': descriptions,
            'global_vector_sha256': hashlib.sha256(vectors[0].tobytes()).hexdigest(),
            'candidate_vector_sha256': [hashlib.sha256(v.tobytes()).hexdigest() for v in vectors[1:]]}


class TextPpoHead(TinyPpo):
    @classmethod
    def warm(cls, legacy):
        model = cls()
        # Old row order survives; added pretrained-text rows start exactly at zero.
        for group in ('parameters', 'm', 'v'):
            old = getattr(legacy, group)
            new = {k: v.copy() for k, v in old.items()}
            new['aw'] = np.zeros((GLOBAL+LOCAL, 64))
            new['aw'][:96] = old['aw'][:96]
            new['aw'][GLOBAL:GLOBAL+64] = old['aw'][96:]
            new['vw'] = np.zeros((GLOBAL, 64))
            new['vw'][:96] = old['vw']
            setattr(model, group, new)
        model.rng.bit_generator.state = copy.deepcopy(legacy.rng.bit_generator.state)
        model.updates, model.adam_steps = legacy.updates, legacy.adam_steps
        return model

    def forward(self, g, c):
        if g.shape != (GLOBAL,) or c.ndim != 2 or c.shape[1] != LOCAL:
            raise ValueError('Frozen text input dimensions mismatch')
        p = self.parameters
        joint = np.concatenate((np.broadcast_to(g, (len(c), GLOBAL)), c), axis=1)
        hidden = np.tanh(joint @ p['aw']+p['ab'])
        logits = hidden @ p['ao']
        probs = np.exp(logits-logits.max()) if len(logits) else np.zeros(0)
        if len(probs):
            probs /= probs.sum()
        vh = np.tanh(g @ p['vw']+p['vb'])
        return probs, float(vh @ p['vo']+p['vob'][0]), (joint, hidden, vh)

    def save_bound(self, directory, contract):
        self.save(directory)
        path = Path(directory)/'metadata.json'
        metadata = json.loads(path.read_text(encoding='utf-8'))
        metadata['schema'] = contract
        metadata['reader_frozen'] = True
        metadata['source_sha256'].update({str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in (Path(__file__), Path(__file__).with_name('frozen_ui_text.py'))})
        write_json(path, metadata)

    @classmethod
    def load_bound(cls, directory, contract, allow_scope_change=False):
        metadata = json.loads((Path(directory)/'metadata.json').read_text(encoding='utf-8'))
        expected = json.loads(json.dumps(contract))
        if metadata['schema'] != expected:
            without_extra = {k: v for k, v in expected.items() if k != 'extra_ui_scope'}
            if not allow_scope_change or metadata['schema'] != without_extra:
                raise ValueError('Encoder revision, text data or input contract differs')
        model = cls.warm(TinyPpo())
        with np.load(Path(directory)/'model.npz', allow_pickle=False) as arrays:
            for key in model.parameters:
                for group, prefix in (('parameters', ''), ('m', 'm_'), ('v', 'v_')):
                    value = arrays[prefix+key]
                    if value.shape != getattr(model, group)[key].shape or not np.isfinite(value).all():
                        raise ValueError('Checkpoint shape/nonfinite mismatch')
                    getattr(model, group)[key] = value.copy()
        model.rng.bit_generator.state = metadata['rng']
        model.updates, model.adam_steps = metadata['updates'], metadata['adam_steps']
        return model


def rollout(model, reader, directory, horizon, frozen=False):
    demo = Demo(directory)
    trace, samples = [], []
    try:
        observation = demo.original_opening()
        for index in range(horizon):
            g, c, controls, texts = reader(observation)
            if not controls or state(observation).get('phase') == 'End':
                break
            probabilities, value, _ = model.forward(g, c)
            choice = int(model.rng.choice(len(controls), p=probabilities))
            result = demo.call('click', observation, controls[choice], screenshot=index%5==0)
            after = result['observation']
            score, done = reward(observation, after), state(after).get('phase') == 'End'
            samples.append({'g': g, 'c': c, 'action': choice, 'old_logprob': float(np.log(probabilities[choice])),
                            'value': value, 'reward': score, 'done': done})
            trace.append({'index': index, 'role': 'frozen_policy' if frozen else 'training_policy',
                'request_id': result['id'], 'before_version': observation['version'],
                'candidate_paths': [control['path'] for control in controls], 'probabilities': probabilities.tolist(),
                'selected': controls[choice], 'action_index': choice, 'value': value,
                'reward': score, 'done': done, 'texts': texts, 'actual': result})
            write_json(Path(directory)/'trajectory.json', {'schema': reader.contract, 'steps': trace})
            observation = after
            if done:
                break
        g, c, _, _ = reader(observation)
        bootstrap = 0 if samples and samples[-1]['done'] else model.forward(g, c)[1]
        write_json(Path(directory)/'summary.json', {'steps': len(samples), 'phase': state(observation).get('phase'),
            'won': state(observation).get('won'), 'return': sum(s['reward'] for s in samples),
            'terminated': bool(samples and samples[-1]['done']), 'bootstrap': bootstrap,
            'stop_reason': 'native_end' if samples and samples[-1]['done'] else
                'step_limit' if len(samples)==horizon else 'no_supported_clickable_controls',
            'scope': 'frozen text/UI PPO interface and update only, not improved policy/rules understanding'})
        return samples, bootstrap
    finally:
        demo.close()


def verify_trace(model, reader, path):
    data = json.loads(Path(path).read_text(encoding='utf-8'))
    clicks, maximum_error = [], 0.
    for row in data['steps']:
        g, c, controls, texts = reader(row['actual']['before'])
        assert texts == row['texts']
        assert [control['path'] for control in controls] == row['candidate_paths']
        probabilities, value, _ = model.forward(g, c)
        maximum_error = max(maximum_error, float(np.max(np.abs(probabilities-row['probabilities']))))
        assert maximum_error < 1e-12 and abs(value-row['value']) < 1e-12
        assert int(model.rng.choice(len(controls), p=probabilities)) == row['action_index']
        click = row['actual']['click']
        assert click['buttonPath'] == controls[row['action_index']]['path']
        assert row['actual']['before']['version'] == row['before_version']
        clicks.append(click)
    comparison = compare_report({'status': 'success', 'steps': clicks})
    assert comparison['status'] == 'passed', comparison
    return {'steps': len(clicks), 'maximum_probability_error': maximum_error,
            'rng_selection_identical': True, 'shared_state_comparison': comparison}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    parser.add_argument('--legacy-checkpoint')
    parser.add_argument('--checkpoint')
    parser.add_argument('--updates', type=int, default=2)
    parser.add_argument('--horizon', type=int, default=32)
    parser.add_argument('--frozen', action='store_true')
    parser.add_argument('--local-only', action='store_true')
    parser.add_argument('--deed-inputs', action='store_true')
    parser.add_argument('--change-ui-scope', action='store_true')
    args = parser.parse_args()
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    encoder = FrozenTextEncoder(ROOT/'AutomationOutputs/StructuredUiPpo/frozen_text_cache', args.local_only)
    reader = FrozenTextFeatures(encoder, args.deed_inputs)
    if args.checkpoint:
        model = TextPpoHead.load_bound(args.checkpoint, reader.contract, args.change_ui_scope)
        if args.change_ui_scope:
            prior = json.loads((Path(args.checkpoint)/'metadata.json').read_text(encoding='utf-8'))
            write_json(out/'explicit_scope_migration.json', {'prior_checkpoint': str(Path(args.checkpoint).resolve()),
                'prior_contract': prior['schema'], 'new_contract': reader.contract,
                'change': 'add actual DeedInputs visible controls only; parameters/Adam/RNG unchanged'})
    else:
        legacy = TinyPpo.load(args.legacy_checkpoint) if args.legacy_checkpoint else TinyPpo()
        model = TextPpoHead.warm(legacy)
    model.save_bound(out/'initial', reader.contract)
    for index in range(args.updates):
        before = TextPpoHead.load_bound(out/'initial' if index==0 else out/f'checkpoint_{index:03d}', reader.contract)
        samples, bootstrap = rollout(model, reader, out/f'episode_{index:03d}', args.horizon, args.frozen)
        assert samples
        mapping = verify_trace(before, reader, out/f'episode_{index:03d}/trajectory.json')
        update = {'samples': len(samples), 'frozen': True} if args.frozen else model.update(samples, bootstrap)
        model.save_bound(out/f'checkpoint_{index+1:03d}', reader.contract)
        update['new_text_actor_nonzero'] = int(np.count_nonzero(model.parameters['aw'][96:GLOBAL])) + int(
            np.count_nonzero(model.parameters['aw'][GLOBAL+64:]))
        write_json(out/f'update_{index+1:03d}.json', {'update': update, 'mapping': mapping})
        print(json.dumps(update), flush=True)
    write_json(out/'encoder_frozen_verification.json', encoder.verify_frozen())


if __name__ == '__main__':
    main()
