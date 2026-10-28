"""Seven archived formal entries with current symbolic UI; local evidence only.

Compare native state and original art to the historical same-task baseline.
Do not compare old prose text trees to the intentionally replaced symbol tree.
"""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover, check_unit_names
from capture_phase_baseline import design_default
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--live', type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True)
    collector = Collector(args.out.resolve(), args.live.resolve())
    rows = []
    bounds_failures = []
    for hero in ['Arythea', 'Braevalar', 'Goldyx', 'Krang', 'Norowas', 'Tovak', 'Wolfhawk']:
        fixture = json.loads((BASE / 'attempt61/before/setups' / (hero + '.json')).read_text(encoding='utf-8'))
        save(args.out / 'setups' / (hero + '.json'), fixture)
        collector.demo.reset_task(fixture['task'])
        collector.demo.prepare_prefix([FIGHT])
        collector.demo.design(design_default())
        collector.rpc('observe')
        dismiss_hover(collector)
        actual = collector.snapshot(hero)
        before = json.loads((BASE / 'attempt61/before/key_phases' / hero / 'capture.json').read_text(encoding='utf-8'))
        assert state(actual['observation'])['audit.costNativeState'] == state(before['observation'])['audit.costNativeState'], hero
        assert (actual['observation']['screenWidth'], actual['observation']['screenHeight']) == (1920, 1080)
        old = {image['path']: image for image in before['evaluation']['rawImages']}
        new = {image['path']: image for image in actual['evaluation']['rawImages']}
        assert old.keys() == new.keys(), hero
        for path, image in new.items():
            for field in ['texture', 'uvRect', 'material', 'atmosphereSaturation', 'atmosphereTop', 'atmosphereGround']:
                assert image[field] == old[path][field], (hero, path, field)
            for dimension in ['width', 'height']:
                if path.endswith('Btn_HeroActor/PartyFigure'):
                    assert image['screenRect'][dimension] > old[path]['screenRect'][dimension]
                elif '/Btn_HeroActor/' in path:
                    # Cast shadow width follows the actor, but ground depth is
                    # deliberately fixed. It is not the character artwork.
                    assert image['screenRect'][dimension] >= old[path]['screenRect'][dimension] - .01
                else:
                    assert abs(image['screenRect'][dimension] - old[path]['screenRect'][dimension]) < .01
            if path.endswith(('/PartyFigure', '/EnemyIllustration')):
                r = image['screenRect']
                if not (r['x'] >= -.01 and r['y'] >= -.01 and r['x'] + r['width'] <= 1920.01 and r['y'] + r['height'] <= 1080.01):
                    bounds_failures.append(dict(hero=hero, path=path, rect=r))
                assert abs(image['uvRect']['width']) == 1 and image['uvRect']['height'] == 1
                assert image['shaderSupported']
        names = check_unit_names(actual)
        figure = next(image for path, image in new.items() if path.endswith('Btn_HeroActor/PartyFigure'))
        rows.append(dict(hero=hero, same_native=True, original_art_and_material_retained=True,
                         hero_rect=figure['screenRect'], visible_unit_labels=[t['fullText'] for t in names]))
        print(hero, 'same native; full art retained', flush=True)
    save(args.out / 'manifest.json', dict(passed=not bounds_failures, bounds_failures=bounds_failures, rows=rows, captures=collector.captures,
         scope='Seven archived same-task formal Ranged entries. Larger hero geometry, original images/materials/full UV, other image dimensions and visible unit identity labels checked. Not silhouette non-overlap, all phases, all army sizes, aesthetic hierarchy or human recognition acceptance.'))
    assert not bounds_failures, bounds_failures


if __name__ == '__main__':
    main()
