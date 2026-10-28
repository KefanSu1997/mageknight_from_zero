"""Continue the real quick-source validation and verify presentation-only result recall."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save, load_fixtures
from structured_ui_ppo import state


TOKENS = ['凝结', '移动力', '影响力', '格挡', '攻击', '+2']


def native(c): return json.loads(state(c.observation)['audit.costNativeState'])


def body(capture):
    return next(t for t in capture['evaluation']['texts'] if t['name']=='ResolutionFullText')


def check_full(capture):
    t=body(capture)
    if any(token not in t['fullText'] for token in TOKENS): raise AssertionError('Full result missing condition')
    if t['fontSize']!=22 or t['isTextOverflowing']: raise AssertionError('Result reduced or ellipsized')
    return dict(font_size=t['fontSize'],full_text=t['fullText'],tmp_overflow=t['isTextOverflowing'],
                scope='Generated full text; actual visible viewport and scrolling require screenshot review')


def run(out, live):
    c=Collector(out,live)
    if not any(x['name']=='Btn_ResolutionRecall' and x['clickable'] for x in c.observation['controls']):
        raise AssertionError('Run verify_quick_source_cycle first, no intervening fixture/reset')
    initial=native(c)
    c.click('Btn_ResolutionRecall'); complete=check_full(c.snapshot('complete_after_rejection'))
    if native(c)!=initial: raise AssertionError('Recall mutated rules')
    c.click('Btn_ResolutionRecall'); c.snapshot('returned_to_action')
    if native(c)!=initial: raise AssertionError('Return mutated rules')
    card=next(x for x in c.observation['controls'] if x['name'].startswith('Btn_Card_') and x['clickable'])
    c.click(card['name']); selected=c.snapshot('selected_action_context')
    if any(t['name']=='ResolutionFullText' for t in selected['evaluation']['texts']):
        raise AssertionError('Recall masks newly selected action')
    selection=native(c)
    c.click('Btn_ResolutionRecall'); check_full(c.snapshot('recall_with_pending_selection'))
    c.click('Btn_ResolutionRecall')
    if native(c)!=selection: raise AssertionError('Toggling lost formal selection')
    # A real sideways use consumes a second physical card; no fixture prefix is counted as this input.
    sideways=next((x for x in c.observation['controls'] if x['name'].startswith('Btn_Sideways') and x['clickable']),None)
    if sideways is None: raise AssertionError('Selected card has no actual sideways control')
    c.click(sideways['name'])
    if not any(t['key']=='ui.Heading' and '已结算' in t['value'] for t in c.observation.get('state',[])):
        # Selected targets may keep action context; explicit recall remains valid.
        if not any(x['name']=='Btn_ResolutionRecall' and x['clickable'] for x in c.observation['controls']):
            raise AssertionError('No recall after next consumption')
    cap=c.snapshot('retained_after_next_consumer')
    if not any(t['name']=='ResolutionFullText' for t in cap['evaluation']['texts']):
        c.click('Btn_ResolutionRecall');cap=c.snapshot('retained_after_next_consumer_opened')
    retained=check_full(cap)
    if '历史记录' not in retained['full_text']: raise AssertionError('Earlier formal result overwritten')
    before_phase=state(c.observation).get('battle.phase')
    c.click('Btn_Next')
    c.click('Btn_ResolutionRecall'); phase=check_full(c.snapshot('retained_after_phase_change'))
    c.prepare(next(f for f in load_fixtures() if f['label']=='outside'))
    fresh=c.snapshot('new_fixture_without_old_result')
    if any(x['name']=='Btn_ResolutionRecall' for x in c.observation['controls']):
        raise AssertionError('History leaked into unrelated fixture')
    result=dict(passed=True,recall_and_return_preserve_native_state=True,selection_context_restored=True,
        result_retained_after_second_consumer=True,result_retained_after_phase_change=True,
        new_session_clears_history=True,complete_result=complete,
        captures=c.captures,actual_pointer_events=len(c.events),
        scope='Fixed actual pointer cases; no all-rule or human-performance claim. Scroll visibility separately inspected.')
    save(out/'validation.json',result)
    return {k:v for k,v in result.items() if k not in ('captures','complete_result')}


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',type=Path,required=True);parser.add_argument('--live',type=Path,required=True)
    args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    try: print(json.dumps(run(args.out.resolve(),args.live.resolve()),ensure_ascii=False))
    except Exception as error:
        save(args.out/'failure.json',dict(error=repr(error)));raise
