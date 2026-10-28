"""Actual saved UI observation gates for frozen reader and warm PPO head."""
import argparse
import copy
import json
from pathlib import Path
import tempfile
import numpy as np
from structured_ui_ppo import ROOT, TinyPpo, features, candidates, write_json, NUMERIC_KEYS, TEXT_KEYS
from frozen_ui_text import FrozenTextEncoder
from train_frozen_text_ui_ppo import FrozenTextFeatures, TextPpoHead, GLOBAL


def verify(out):
    encoder = FrozenTextEncoder(ROOT/'AutomationOutputs/StructuredUiPpo/frozen_text_cache', True)
    reader = FrozenTextFeatures(encoder)
    legacy = TinyPpo.load(ROOT/'AutomationOutputs/StructuredUiPpo/20261011/train_verified/checkpoint_002')
    model = TextPpoHead.warm(legacy)
    files = sorted((ROOT/'AutomationOutputs/StructuredUiPpo/20261011/train_verified/episode_000/exchanges').glob('*.json'))
    observations = [json.loads(p.read_text(encoding='utf-8'))['response']['observation'] for p in files]
    observations = [o for o in observations if candidates(o)][:6]
    maximum = 0.
    samples = []
    for observation in observations:
        old_g, old_c, _ = features(observation)
        g, c, controls, texts = reader(observation)
        p, value, _ = model.forward(g, c)
        old_p, old_value, _ = legacy.forward(old_g, old_c)
        maximum = max(maximum, float(np.max(np.abs(p-old_p))), abs(value-old_value))
        assert maximum < 1e-12
        assert any('原印刷规则' in t for t in texts['candidate_texts'])
        altered = copy.deepcopy(observation)
        for row in altered['state']:
            if row['key'] not in ('phase', *NUMERIC_KEYS, *TEXT_KEYS):
                row['value'] = 'unknown audit state must never reach actor'
        ag, ac, _, at = reader(altered)
        np.testing.assert_array_equal(g, ag)
        np.testing.assert_array_equal(c, ac)
        assert texts == at
        samples.append({'g': g, 'c': c, 'action': 0, 'old_logprob': float(np.log(p[0])),
                        'value': value, 'reward': .1*len(samples), 'done': False})
    model.save_bound(Path(out)/'warm_checkpoint', reader.contract)
    restored = TextPpoHead.load_bound(Path(out)/'warm_checkpoint', reader.contract)
    for group in ('parameters', 'm', 'v'):
        for key, array in getattr(model, group).items():
            np.testing.assert_array_equal(array, getattr(restored, group)[key])
    np.testing.assert_array_equal(model.rng.random(10), restored.rng.random(10))
    mismatch = copy.deepcopy(reader.contract)
    mismatch['encoder']['revision'] = 'wrong revision'
    try:
        TextPpoHead.load_bound(Path(out)/'warm_checkpoint', mismatch)
        raise AssertionError('Wrong encoder version was accepted')
    except ValueError:
        pass
    # Numerical gate on actual UI inputs, separately labelled and never native PPO history.
    advantages, targets = np.linspace(-.5, .7, len(samples)), np.zeros(len(samples))
    _, gradient = model.loss_gradient(samples, advantages, targets)
    worst = 0.
    for key, index in [('aw', (96, 0)), ('aw', (GLOBAL+64, 0)), ('vw', (96, 0)), ('ao', (0,))]:
        array = model.parameters[key]
        old = array[index]
        array[index] = old+1e-6
        plus = model.loss_gradient(samples, advantages, targets)[0]
        array[index] = old-1e-6
        minus = model.loss_gradient(samples, advantages, targets)[0]
        array[index] = old
        worst = max(worst, abs((plus-minus)/2e-6-gradient[key][index]))
    assert worst < 1e-7
    result = {'status': 'passed', 'actual_observations': len(observations),
        'warm_full_probability_value_max_error': maximum, 'numeric_gradient_max_error': worst,
        'head_parameter_count': sum(p.size for p in model.parameters.values()),
        'contract': reader.contract, 'hidden_fields_isolated': True,
        'parameter_optimizer_rng_restore_identical': True, 'wrong_encoder_rejected': True,
        'encoder_frozen': encoder.verify_frozen(),
        'scope': 'input/migration/numerical gates only; no new actual PPO update or learned capability'}
    write_json(Path(out)/'entry_verification.json', result)
    print(json.dumps({k: v for k, v in result.items() if k != 'contract'}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    verify(parser.parse_args().out)
