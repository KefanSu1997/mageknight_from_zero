"""Sole-operator cleanup between completed fixture capture and the next RSI round."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import time

from capture_phase_baseline import ROOT, design_default


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--experiment', type=Path, required=True)
    parser.add_argument('--phase-dir', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    experiment, phase = args.experiment.resolve(), args.phase_dir.resolve()
    phase.relative_to(experiment)
    manifest = json.loads((phase / 'manifest.json').read_text(encoding='utf-8-sig'))
    if manifest.get('passed') is not True or not manifest.get('captures'):
        raise ValueError('Successful actual phase capture required before restoration')
    receipt_path = phase / 'normal_launch_restored.json'
    if receipt_path.exists():
        raise FileExistsError(receipt_path)
    live = json.loads((experiment / 'live_status.json').read_text(encoding='utf-8-sig'))
    request = Path(live['request']).resolve()
    request.relative_to(experiment / 'model_calls')
    if (live.get('status') != 'waiting_for_model'
            or (request.parent / 'response.json').exists()
            or json.loads(request.read_text(encoding='utf-8-sig')).get('kind') != 'reader'):
        raise ValueError('Hold the next unanswered reader request before runtime cleanup')
    if not args.execute:
        print(json.dumps(dict(validated=True, live_operations=0, output=str(receipt_path))))
        return
    # Import live operations only after the explicit execution gate.
    from capture_ui_design_pairs import editor_call
    from unity_adapter import capture_demo

    deadline = time.monotonic() + 600
    editor_call('stop', timeout=max(1, deadline - time.monotonic()))
    while editor_call('status', timeout=max(1, deadline - time.monotonic())).get('isPlaying'):
        if time.monotonic() >= deadline:
            raise TimeoutError('Pilot did not stop; no restoration claimed')
        time.sleep(.2)
    launch = editor_call('start_pilot', timeout=max(1, deadline - time.monotonic()))
    demo = capture_demo(experiment / 'baseline/live', attach=True)
    observed = demo.call('observe', screenshot=True)
    observation = observed['observation']
    if not any(c['name'] == 'Btn_BattleHero' and c['clickable'] for c in observation['controls']):
        raise AssertionError('Fresh normal hero selection was not observed')
    status = editor_call('status', timeout=max(1, deadline - time.monotonic()))
    if not (status['isPlaying'] and not status['isCompiling'] and status['actualConsoleErrorCount'] == 0):
        raise AssertionError('Normal pilot not ready, or Console contains errors')
    screenshot = phase / 'normal_launch_restored.png'
    if screenshot.exists():
        raise FileExistsError(screenshot)
    shutil.copyfile(observed['screenshotPath'], screenshot)
    receipt = dict(role='fresh_normal_launch_after_fixture_capture',
        initial_version=observation['version'], launch=launch, runtime_status=status,
        ui_source_sha256=hashlib.sha256((ROOT / 'Assets/Scripts/Battle/PveBattleController.cs').read_bytes()).hexdigest(),
        source_default_optimizedInteractionUi=design_default(), actual_normal_hero_selection_observed=True,
        screenshot=str(screenshot), screenshot_sha256=hashlib.sha256(screenshot.read_bytes()).hexdigest(),
        scope='Actual fresh normal hero selection after native fixtures; never resume fixture state into RSI play.')
    with receipt_path.open('x', encoding='utf8') as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(restored=True, initial_version=observation['version'])))


if __name__ == '__main__':
    main()
