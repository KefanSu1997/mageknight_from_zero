"""Revisit the original RSI pre-iteration fixtures in the current Unity UI.

Original PNGs remain unchanged. Setup is not a player action sequence or cost
measurement; keep every attempted context and any native-state mismatch.
"""
import argparse
import hashlib
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state

ROOT = Path(__file__).resolve().parents[2]
ORIGINAL = ROOT / 'AutomationOutputs/RsiGame/experiments/20261021_mageknight_unity_30/baseline/key_phases'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--live', type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True)
    fixtures = json.loads((ORIGINAL / 'fixtures.json').read_text(encoding='utf-8-sig'))['fixtures']
    save(args.out / 'registration.json', dict(labels=[f['label'] for f in fixtures],
        baseline=str(ORIGINAL), scope='Original RSI pre-round1 baseline, not attempt63 black-plate intermediate. All ten original finite fixtures; no dynamic cost claims.'))
    collector = Collector(args.out.resolve(), args.live.resolve())
    rows = []
    for fixture in fixtures:
        label = fixture['label']
        try:
            collector.prepare(fixture)
            dismiss_hover(collector)
            capture = collector.snapshot(label)
            old = json.loads((ORIGINAL / label / 'observation.json').read_text(encoding='utf-8-sig'))
            same = state(capture['observation'])['audit.costNativeState'] == state(old['observation'])['audit.costNativeState']
            baseline = ORIGINAL / label / 'screen.png'
            rows.append(dict(label=label, captured=True, exact_native_equal=same,
                             original_image=str(baseline), original_sha256=hashlib.sha256(baseline.read_bytes()).hexdigest(),
                             current=collector.captures[-1], comparison_limit=None if same else 'Current rules projection differs; do not treat as exact-state paired comparison.'))
            print(label, 'captured; exact native equal:', same, flush=True)
        except Exception as error:
            rows.append(dict(label=label, captured=False, error=repr(error)))
            print(label, 'failed:', repr(error), flush=True)
        save(args.out / ('result_' + label + '.json'), rows[-1])
    save(args.out / 'manifest.json', dict(passed=all(r.get('captured') and r.get('exact_native_equal') for r in rows),
        rows=rows, captures=collector.captures, scope='Ten original registered contexts. Exact-state comparison only where confirmed; screenshots alone do not prove costs, rules consumers, latency or human performance.'))


if __name__ == '__main__':
    main()
