"""Actual six inventory information surfaces; no source/payment consumption claim."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt59/mana_readers';c=Collector(out.resolve(),(BASE/'attempt59/live_review').resolve())
    c.prepare(json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture']);rows=[]
    for color in ['Red','Blue','Green','White','Gold','Black']:
        native=state(c.observation)['audit.costNativeState'];path='Canvas/BattleTable/ManaInventory_'+color
        c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot(color)
        panel=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='BattleHoverInfo')
        texts=[t for t in r['evaluation']['texts'] if '/BattleHoverInfo/' in t['path']]
        for t in texts:
            assert t['fontSize']>=21 and not t['isTextOverflowing']
            assert {i for i,ch in enumerate(t['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']})
            assert all(inside(g['screenRect'],panel,.1) for g in t['glyphs'])
        assert state(c.observation)['audit.costNativeState']==native
        rows.append(dict(color=color,texts=[t['fullText'] for t in texts],panel=panel,all_glyphs_visible=True))
        c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);dismiss_hover(c)
    save(out/'manifest.json',dict(passed=True,rows=rows,scope='6actual inventory hover surfaces, full21/22/24pt glyphs within folio and unchanged native state. No public source take, actual payment or global mana design acceptance.'))
    print('6mana inventory folio readers passed')
if __name__=='__main__':main()
