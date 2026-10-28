"""Targeted actual hover evidence for the alpha-only reading-well patch."""
import json
from collections import Counter
from pathlib import Path
from PIL import Image
from capture_evaluation_cycle import Collector, save, ROOT
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def main():
    base = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
    out = base / 'attempt22/validation'
    out.mkdir(parents=True, exist_ok=False)
    c = Collector(out, base / 'attempt22/live')
    try:
        fixture = json.loads((base / 'attempt21/validation/setups/three_enemies.json').read_text(encoding='utf8'))['fixture']
        c.prepare(fixture)
        c.click('Btn_UnitActor_1')
        dismiss_hover(c)
        c.snapshot('before_hover')
        before = state(c.observation)['audit.costNativeState']
        card = next(x for x in c.observation['controls'] if x['name'].startswith('Btn_Card_') and x['clickable'])
        c.rpc('hover', path=card['path'], expectedVersion=c.observation['version'])
        response = c.snapshot('opaque_card_reading')
        assert state(c.observation)['ui.hoverPreviewVisible'] == 'True'
        assert state(c.observation)['audit.costNativeState'] == before
        im = Image.open(response['screenshotPath']).convert('RGB')
        pixels = im.load()
        histogram = Counter(pixels[x, y] for y in range(240, 430) for x in range(1420, 1460))
        assert len(histogram) == 1, 'Underlying text is still visible through reading-well margin'
        c.rpc('unhover', path=card['path'], expectedVersion=c.observation['version'])
        c.snapshot('after_unhover')
        assert state(c.observation)['ui.hoverPreviewVisible'] == 'False'
        assert state(c.observation)['audit.costNativeState'] == before
        save(out / 'manifest.json', dict(passed=True, parent_full_validation='attempt21/validation/party_feedback_manifest.json',
            change_scope='Only reading-well alpha 250->255; full standard suite belongs to parent version, not rerun for this patch.',
            captures=c.captures, native_state_unchanged=True, hover_open_and_exit=True,
            opaque_margin_pixel_histogram=[dict(rgb=rgb, count=n) for rgb, n in histogram.items()],
            pixel_scope='Untouched actual Unity screenshot, 40x190 margin outside card; inspection only, no image modification.'))
    except Exception as error:
        save(out / 'failure.json', dict(error=repr(error), captures=c.captures))
        raise


if __name__ == '__main__':
    main()
