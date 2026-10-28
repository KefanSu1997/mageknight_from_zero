"""Actual card-mode comparison: full copy or explicitly disclosed count fallback."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_entity_hover_reader import compare_actions


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['out','live','fixture']:p.add_argument('--'+name,type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    c=Collector(args.out.resolve(),args.live.resolve())
    c.prepare(json.loads(args.fixture.read_text(encoding='utf8'))['fixture'])
    card=next(b for b in c.observation['controls'] if b['name'].startswith('Btn_Card_') and b['clickable'])
    c.click(card['name']);dismiss_hover(c);source=c.snapshot('card_selected_before');prior=state(c.observation)
    actor=next(b for b in c.observation['controls'] if b['name']=='Btn_HeroActor')
    c.rpc('hover',path=actor['path'],expectedVersion=c.observation['version']);record=c.snapshot('card_selected_hover_hero')
    text=next(t for t in record['evaluation']['texts'] if t['name']=='SelectedActionComparison')
    buttons=[b for b in source['observation']['controls'] if '/Inspector/' in b['path'] and '/Actions/' in b['path']]
    if text['fullText'].startswith('已选有 '):
        assert text['fullText']=='已选有 '+str(len(buttons))+' 项操作。\n移开后查看全部操作与费用。'
        mode='explicit_count_fallback'
    else:
        compare_actions(record,source);mode='full_copy'
    assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
    assert state(c.observation)['audit.costFixture']==prior['audit.costFixture']
    c.rpc('unhover',path=actor['path'],expectedVersion=c.observation['version']);returned=c.snapshot('card_selected_returned')
    def signature(r):return {b['path']:(b['interactable'],b['clickable']) for b in r['observation']['controls']}
    assert signature(source)==signature(returned)
    save(args.out/'manifest.json',dict(passed=True,card_control=card,mode=mode,native_option_count=len(buttons),display=text['fullText'],same_native_and_selection=True,normal_control_signature_restored=True,captures=c.captures,scope='One actual selected-card context; explicit fallback is not simultaneous full cost/effect comparison for all options.'))
    print(mode,len(buttons),text['fullText'])


if __name__=='__main__':main()
