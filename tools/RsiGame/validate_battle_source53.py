"""Selected-action continuity, real source-extra consumer and die geometry."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_phase_baseline import load_fixtures,compare
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_entity_hover_reader import inside
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt53'
def texts(record):
    rows=[]
    for t in record['evaluation']['texts']:
        if not (t['name'] in ['ManaColorIdentity','ManaHoverCue','SourceHeading','SourceQuota','SourceDieIdentity'] or '/BattleSource/' in t['path'] and t['name']=='Label'):continue
        if not t['fullText']:continue
        assert t['fontSize']>=22 and not t['isTextOverflowing'],t['path']
        expected={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
        assert expected.issubset({g['sourceIndex'] for g in t['glyphs']}),(t['path'],t['fullText'])
        assert all(inside(g['screenRect'],t['screenRect'],3) for g in t['glyphs']),t['path']
        rows.append(dict(path=t['path'],text=t['fullText'],font=t['fontSize']))
    return rows
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--out',type=Path,required=True);ap.add_argument('--live',type=Path,required=True);args=ap.parse_args()
    out=args.out.resolve();live=args.live.resolve();c=Collector(out/'continuity',live)
    f=next(f for f in load_fixtures() if f['label']=='source_play');c.prepare(f);dismiss_hover(c)
    # Pick an actual hand card, then retain its current mode and target context.
    card=next(x for x in c.observation['controls'] if x['clickable'] and x['name'].startswith('Btn_Card_'))
    c.click(card['name']);dismiss_hover(c);r=c.snapshot('selected_action_before');texts(r)
    native=state(c.observation)['audit.costNativeState'];pose=state(c.observation)['audit.costFixture']
    for label,detail in [('cancel',False),('details',True)]:
        c.click('Btn_QuickSource');dismiss_hover(c);r=c.snapshot(label+'_source_open');texts(r)
        assert state(c.observation)['audit.costNativeState']==native
        dice=[x for x in c.observation['controls'] if x['name'].startswith('Btn_Source_') and x['clickable']]
        assert len(dice)==min(6,len(json.loads(native)['auditSourceOrder']))
        c.click(dice[0]['name']);dismiss_hover(c);r=c.snapshot(label+'_die_selected');texts(r)
        assert state(c.observation)['audit.costNativeState']==native
        if detail:
            c.click('Btn_SourceTacticDetails');dismiss_hover(c);c.snapshot('full_tactic_details')
            assert state(c.observation)['audit.costNativeState']==native
        c.click('Btn_CloseTools');dismiss_hover(c);r=c.snapshot(label+'_returned')
        assert state(c.observation)['audit.costNativeState']==native
        assert state(c.observation)['audit.costFixture']==pose,'Selected card/targets/current UI arguments changed on source inspection'
    save(out/'continuity/manifest.json',dict(passed=True,captures=c.captures,selected_card=card['name'],source_inspection_no_native_change=True,selected_context_retained=True))
    consumers=[]
    for label,filename in [('extra','extra_source_fixture.json'),('extra_black_wild','extra_black_wild_fixture.json'),('extra_gold','extra_gold_fixture.json')]:
        c=Collector(out/label,live);fixture=json.loads((OUT/filename).read_text());c.prepare(fixture);dismiss_hover(c)
        before=state(c.observation)['audit.costNativeState'];c.click('Btn_QuickSource');dismiss_hover(c);r=c.snapshot('extra_before');texts(r)
        color=['Red','Blue','Green','White','Gold','Black'][fixture['action']['Targets']]
        index=state(c.observation)['source.faces'].split(',').index(color);c.click('Btn_Source_'+str(index));dismiss_hover(c)
        if color=='Black' and fixture['action']['Option']!=5:
            chosen=['Red','Blue','Green','White','Gold','Black'][fixture['action']['Option']];c.click('Btn_SourceColor_'+chosen);dismiss_hover(c)
        r=c.snapshot('extra_selected');texts(r)
        assert state(c.observation)['audit.costNativeState']==before
        c.click('Btn_UseExtraSource');dismiss_hover(c);r=c.snapshot('extra_consumed');_,diffs=compare(fixture['expected_after'],c.observation);assert not diffs,diffs
        after=json.loads(state(c.observation)['audit.costNativeState']);assert after['extraDice']==json.loads(before)['extraDice']-1
        c.click('Btn_QuickSource');dismiss_hover(c);r=c.snapshot('quota_after_extra');texts(r)
        b=next(x for x in c.observation['controls'] if x['name']=='Btn_UseExtraSource');assert b['interactable']==(after['extraDice']>0)
        save(out/label/'manifest.json',dict(passed=True,captures=c.captures,actual_action=fixture['action'],provenance=fixture['provenance'],native_after_matches_archive=True,after=after));consumers.append(label)
    save(out/'manifest.json',dict(passed=True,consumers=consumers,scope='Selected-card context survives source select/cancel and full-tactic return. Actual extraWhite, extraBlack→White and extraGold formal consumers match3SHA-bound archived states and quota. Normal source+enhancement separately validated. Not all black-wild cases/tactics, dice counts or human comprehension.'))
    print('53 selected-action continuity and exact extra-source consumer passed',flush=True)
if __name__=='__main__':main()
