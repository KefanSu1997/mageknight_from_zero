"""Selected wounded unit: readiness, activation qualification and actual numeral."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt57/wounded_validation_review_fix';c=Collector(out.resolve(),(BASE/'attempt57/live_review_fix').resolve())
    fixture=json.loads((BASE/'attempt55/before/setups/monster_monster_060.json').read_text(encoding='utf8'))['fixture'];c.prepare(fixture)
    units=json.loads(state(c.observation)['audit.costNativeState'])['units'];i=next(i for i,u in enumerate(units) if u['wounds']>0)
    assert units[i]['ready'];c.click('Btn_UnitActor_'+str(i));dismiss_hover(c);r=c.snapshot('selected_wounded')
    native=state(c.observation)['audit.costNativeState'];values={t['path'].split('/')[-2]:t for t in r['evaluation']['texts'] if t['name']=='Value' and 'Fact/' in t['path']}
    assert values['UnitWoundFact']['fullText']==str(units[i]['wounds']) and values['UnitWoundFact']['hasVisibleGlyphs']
    assert values['UnitReadinessFact']['fullText']=='就绪' and values['UnitCommandFact']['fullText']=='可选0'
    path=values['UnitCommandFact']['path'].rsplit('/',1)[0];c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot('full_status')
    body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
    assert '不能激活' in body['fullText'] and '创伤 '+str(units[i]['wounds']) in body['fullText'] and body['fontSize']>=22
    assert state(c.observation)['audit.costNativeState']==native
    save(out/'manifest.json',dict(passed=True,index=i,actual_wounds=units[i]['wounds'],ready=True,shown_qualification='可选0',reader_body=body['fullText'],
        scope='One archived actual wounded unit, selected with actual pointer; readiness and no current executable operation distinguished, exact native state retained. Not all wounded special abilities.'))
    print('Wounded selected unit:ready!=activation, true numeral and reason preserved')
if __name__=='__main__':main()
