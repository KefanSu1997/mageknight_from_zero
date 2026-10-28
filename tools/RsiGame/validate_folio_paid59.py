"""Actual native-task paid attack/block presentation; no payment-consumer claim."""
import json,copy
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_four_unit_battle_layout import FIGHT
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt59/paid_validation';c=Collector(out.resolve(),(BASE/'attempt59/live_review').resolve())
    task=json.loads((BASE/'attempt56/four_unit_task.json').read_text(encoding='utf8'))
    task['Build']['UnitIds']=['army_003','army_004','army_007','army_2_001']
    save(out/'task.json',task)
    c.observation=c.demo.reset_task(task);c.observation=c.demo.prepare_prefix([FIGHT]);dismiss_hover(c)
    rows=[]
    for label,index,ability,expected in [('red_ranged',0,1,'发动 · 火焰远程 6\n红色魔力'),('blue_siege',1,2,'发动 · 寒冰攻城 5\n蓝色魔力'),('red_block',3,1,'发动 · 火焰格挡 4\n红色魔力')]:
        if label=='red_block':c.click('Btn_Next')
        native=state(c.observation)['audit.costNativeState'];c.click('Btn_UnitActor_'+str(index));dismiss_hover(c);r=c.snapshot(label)
        b=next(b for b in c.observation['controls'] if b['name']=='Btn_UnitAbility_'+str(ability));assert b['text']==expected,(label,b['text'])
        t=next(t for t in r['evaluation']['texts'] if t['path']==b['path']+'/Label');assert t['fontSize']>=22 and t['hasVisibleGlyphs'] and not t['isTextOverflowing']
        assert {i for i,ch in enumerate(expected) if not ch.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']})
        assert state(c.observation)['audit.costNativeState']==native
        rows.append(dict(case=label,actual_caption=b['text'],native_unchanged=True,font_size=t['fontSize']))
    save(out/'manifest.json',dict(passed=True,rows=rows,scope='Formal native task setup plus actual unit selections; paid red/blue, ranged/siege/block captions22pt; no actual paid ability use or cost-consumption proof.'))
    print('3paidattack/block presentations passed')
if __name__=='__main__':main()
