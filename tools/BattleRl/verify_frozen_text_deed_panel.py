"""Bounded real-UI fixture admission, then frozen-policy decisions in DeedInputs."""
import argparse
import json
from pathlib import Path
import numpy as np
from structured_ui_ppo import ROOT, Demo, state, write_json
from frozen_ui_text import FrozenTextEncoder
from train_frozen_text_ui_ppo import FrozenTextFeatures, TextPpoHead, verify_trace


def verify(out):
    out = Path(out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    encoder = FrozenTextEncoder(ROOT/'AutomationOutputs/StructuredUiPpo/frozen_text_cache', True)
    reader = FrozenTextFeatures(encoder, True)
    checkpoint = ROOT/'AutomationOutputs/StructuredUiPpo/20261011_frozen_text/train_live/checkpoint_002'
    for attempt in range(5):
        model = TextPpoHead.load_bound(checkpoint, reader.contract, True)
        demo = Demo(out/f'attempt_{attempt:02d}')
        try:
            observation = demo.original_opening()
            mana_draw = [c for c in observation['controls'] if c['clickable'] and c['name'].startswith('Btn_Card_basic_card_009_')]
            if not mana_draw:
                write_json(demo.directory/'fixture_admission.json', {'status': 'no_target_in_actual_hand',
                    'scope': 'scripted fixture admission, not policy evaluation or training'})
                continue
            prefix = []
            for name in (mana_draw[0]['name'], 'Btn_Choice_base', 'Btn_Play'):
                matches = [c for c in observation['controls'] if c['name']==name and c['clickable']]
                assert len(matches)==1, name
                actual = demo.call('click', observation, matches[0], screenshot=name=='Btn_Play')
                prefix.append({'role': 'scripted_panel_admission_not_policy', 'actual': actual})
                observation = actual['observation']
            write_json(demo.directory/'panel_admission.json', {'steps': prefix})
            model.save_bound(demo.directory/'initial', reader.contract)
            trace = []
            for index in range(12):
                g, c, controls, texts = reader(observation)
                if not any('/DeedInputs/' in control['path'] for control in controls):
                    break
                probabilities, value, _ = model.forward(g, c)
                choice = int(model.rng.choice(len(controls), p=probabilities))
                actual = demo.call('click', observation, controls[choice], screenshot=True)
                trace.append({'index': index, 'role': 'frozen_policy_conditional_panel_fixture',
                    'request_id': actual['id'], 'before_version': observation['version'],
                    'candidate_paths': [control['path'] for control in controls], 'probabilities': probabilities.tolist(),
                    'selected': controls[choice], 'action_index': choice, 'value': value, 'texts': texts, 'actual': actual})
                observation = actual['observation']
                write_json(demo.directory/'trajectory.json', {'schema': reader.contract, 'steps': trace})
            assert trace and not any('/DeedInputs/' in c['path'] for c in reader(observation)[2])
            before = TextPpoHead.load_bound(demo.directory/'initial', reader.contract)
            correspondence = verify_trace(before, reader, demo.directory/'trajectory.json')
            write_json(out/'verification.json', {'status': 'passed', 'attempt': attempt,
                'actual_policy_panel_clicks': len(trace), 'correspondence': correspondence,
                'encoder_frozen': encoder.verify_frozen(),
                'scope': 'conditional scripted panel admission then actual frozen policy clicks; no learning/win claim'})
            print(json.dumps({'status': 'passed', 'attempt': attempt, 'panel_policy_clicks': len(trace)}))
            return
        finally:
            demo.close()
    raise RuntimeError('No actual Mana Draw hand within bounded fixture search; preserve attempts as unknown')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    verify(parser.parse_args().out)
