"""Independent actual UI examples plus strict saved-policy/click correspondence."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from structured_ui_ppo import Demo, TinyPpo, candidates, features, state, write_json
from compare_unity_state_report import compare_report


def negative_examples(directory):
    demo = Demo(directory)
    checks = []

    def reject(observation, control, expected_error):
        before = demo.call('observe')['observation']
        try:
            demo.call('click', observation, control)
            raise AssertionError('A rejected pointer action was executed')
        except RuntimeError as error:
            assert expected_error in str(error), str(error)
        request = json.loads((Path(directory)/'request.json').read_text(encoding='utf-8'))
        response = json.loads((Path(directory)/'responses'/f"{request['id']}.json").read_text(encoding='utf-8'))
        after = demo.call('observe')['observation']
        # Unity JsonUtility serializes null reference fields as an empty object.
        click = response.get('click') or {}
        assert response['success'] is False and not click.get('hitObject') and not click.get('buttonPath')
        assert not click.get('success')
        assert before['version'] == after['version']
        checks.append({'request_id': request['id'], 'expected_error': expected_error,
                       'actual_error': response['error'], 'no_pointer_event': True,
                       'actual_state_unchanged': True})

    try:
        observation = demo.initial_observation
        obscured = next(c for c in observation['controls'] if c['name'].startswith('Btn_Card_'))
        assert not obscured['clickable'], 'Actual opening overlay must obscure the hand'
        reject(observation, obscured, 'obscured')
        observation = demo.original_opening()
        control = next(c for c in candidates(observation) if c['name'] == 'Btn_Next')
        reject({**observation, 'version': 'stale-version-negative-case'}, control, 'Stale observation')
        observation = demo.call('observe')['observation']
        reject(observation, {**control, 'path': control['path']+'/missing-control'}, 'missing or ambiguous')
        write_json(Path(directory)/'negative_verification.json', {'status': 'passed', 'checks': checks,
            'scope': 'three actual fail-closed pointer requests; no policy or training samples'})
    finally:
        demo.close()


def verify_training(directory):
    directory = Path(directory)
    evidence, total = [], 0
    for index, path in enumerate(sorted(directory.glob('episode_*/trajectory.json'))):
        checkpoint = directory/'initial' if index == 0 else directory/f'checkpoint_{index:03d}'
        model = TinyPpo.load(checkpoint)
        trajectory = json.loads(path.read_text(encoding='utf-8'))
        actual_clicks = []
        probability_error = 0.
        for row in trajectory['steps']:
            actual = row['actual']
            before = actual['before']
            assert actual['success'] and actual['click']['success']
            assert before['version'] == row['before_version']
            g, c, controls = features(before)
            paths = [control['path'] for control in controls]
            assert paths == row['candidate_paths']
            assert paths[row['action_index']] == actual['click']['buttonPath'] == row['selected']['path']
            probs, value, _ = model.forward(g, c)
            probability_error = max(probability_error, float(np.max(np.abs(probs-row['probabilities']))))
            assert probability_error < 1e-12
            assert abs(value-row['value']) < 1e-12
            assert int(model.rng.choice(len(probs), p=probs)) == row['action_index']
            assert actual['click']['hitObject']
            assert before['screenWidth'] == 1920 and before['screenHeight'] == 1080
            actual_clicks.append(actual['click'])
        report = compare_report({'status': 'success', 'steps': actual_clicks})
        assert report['status'] == 'passed', report
        total += len(actual_clicks)
        evidence.append({'trajectory': str(path.resolve()), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'clicks': len(actual_clicks), 'shared_state_comparisons': report['comparisons'],
            'maximum_probability_error': probability_error, 'rng_selection_reproduced': True})
    return {'status': 'passed', 'actual_policy_clicks': total, 'episodes': evidence,
            'scope': 'saved policy, full pilot candidate distribution/RNG and actual pointer/state correspondence'}


def examples(directory):
    demo = Demo(directory)
    rows, checks = [], []
    observation = demo.initial_observation

    def click(name, screenshot=False):
        nonlocal observation
        controls = [c for c in observation['controls'] if c['name'] == name and c['clickable']]
        assert len(controls) == 1, (name, controls)
        actual = demo.call('click', observation, controls[0], screenshot=screenshot)
        rows.append({'role': 'scripted_validation_not_policy', 'name': name, 'actual': actual})
        observation = actual['observation']
        write_json(Path(directory)/'examples.json', {'steps': rows, 'checks': checks})
        return actual

    def expect(label, key, expected):
        actual = state(observation)[key]
        checks.append({'label': label, 'key': key, 'expected': str(expected), 'actual': actual,
                       'passed': actual == str(expected)})
        assert actual == str(expected), checks[-1]

    try:
        observation = demo.original_opening()
        click('Btn_Next')
        expect('正式阶段切换', 'phase', 'Block')
        block_start = state(observation)
        card = next(c for c in candidates(observation) if c['name'].startswith('Btn_Card_'))
        click(card['name'])
        expect('选牌只改变选择，不提前消费', 'hand', block_start['hand'])
        click('Btn_Sideways', screenshot=True)
        expect('实际横置离手', 'hand', int(block_start['hand'])-1)
        expect('实际横置格挡贡献', 'block', int(block_start['block'])+1)
        expect('实际已出牌增加', 'played', int(block_start['played'])+1)
        click('Btn_ResetPhase', screenshot=True)
        expect('阶段检查点恢复手牌', 'hand.ids', block_start['hand.ids'])
        expect('阶段检查点恢复格挡', 'block', block_start['block'])
        expect('阶段检查点恢复已出牌', 'played', block_start['played'])
        click('Btn_Next')
        expect('正式承伤阶段', 'phase', 'AssignDamage')
        click('Btn_Next', screenshot=True)
        expect('正式提交承伤进入近战', 'phase', 'Melee')
        melee_start = state(observation)
        for number in range(3):
            card = next(c for c in candidates(observation) if c['name'].startswith('Btn_Card_'))
            click(card['name'])
            click('Btn_Sideways', screenshot=number==2)
            expect('三张真实卡累计近战贡献', 'melee', number+1)
        expect('三张真实卡离手', 'hand', int(melee_start['hand'])-3)
        click('Btn_Target')
        click('Btn_AttackParts')
        click('Btn_All')
        click('Btn_Done')
        click('Btn_Resolve', screenshot=True)
        expect('正式攻击击败原敌', 'kills', 1)
        expect('正式消费三份贡献', 'melee', 0)
        expect('攻击来源消费', 'attacks.sources', '')
        # Single enemy defeated is not the formal End/won condition yet.
        click('Btn_Next', screenshot=True)
        expect('正式战斗收尾', 'phase', 'End')
        expect('正式完整战斗获胜', 'won', 'True')
        clicks = [row['actual']['click'] for row in rows]
        comparison = compare_report({'status': 'success', 'steps': clicks})
        assert comparison['status'] == 'passed', comparison
        write_json(Path(directory)/'verification.json', {'status': 'passed', 'clicks': len(rows),
            'checks': checks, 'shared_state_comparison': comparison,
            'scope': 'scripted real original UI validation; not autonomous PPO victory or full original acceptance'})
    finally:
        write_json(Path(directory)/'examples.json', {'steps': rows, 'checks': checks})
        demo.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--training')
    parser.add_argument('--examples')
    parser.add_argument('--negative')
    args = parser.parse_args()
    if args.training:
        result = verify_training(args.training)
        write_json(Path(args.training)/'correspondence_verification.json', result)
        print(json.dumps(result), flush=True)
    if args.examples:
        examples(args.examples)
        print('Actual UI examples passed', flush=True)
    if args.negative:
        negative_examples(args.negative)
        print('Actual rejected-input examples passed', flush=True)
