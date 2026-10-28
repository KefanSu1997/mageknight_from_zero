"""Fresh-launch normal hero entry and replay the original five real pointer coordinates."""
import argparse
import json
from pathlib import Path
import shutil
from capture_evaluation_cycle import save
from capture_ui_design_pairs import editor_call
from unity_adapter import UnityAdapter,session_class


def run(root, demo):
    editor_call('stop')
    adapter=UnityAdapter(root/'normal_live')
    initial=adapter.boot(attach=False)
    hero=any(x['name']=='Btn_BattleHero' and x['clickable'] for x in adapter.observation['controls'])
    if not hero: raise AssertionError('Actual fresh normal hero selection missing')
    shutil.copyfile(initial['screenshot'],root/'normal_hero_selection.png')
    records=[]
    with session_class(adapter)(Path(__file__).resolve().parents[2],out_dir=root/'normal_opening_events') as session:
        for event in json.loads(demo.read_text(encoding='utf8'))['events']:
            row=session.do('click',dict(x=event['x'],y=event['y']))
            records.append(row)
            if not row.get('ok'): raise AssertionError(row.get('error'))
    final=adapter.observe()
    shutil.copyfile(final['screenshot'],root/'normal_opening.png')
    statuses=[editor_call('status'),editor_call('status')]
    if any(x['actualConsoleErrorCount']!=0 for x in statuses):raise AssertionError('Runtime Console error')
    value=dict(actual_fresh_hero_selection=hero,original_opening_completed=True,
               original_demo=str(demo.resolve()),initial=initial,final=final,events=records,statuses=statuses,
               scope='Normal five-pointer opening; fixture state discarded; not full-game validation.')
    save(root/'normal_restore.json',value)
    return dict(actual_fresh_hero_selection=hero,original_opening_completed=True,console_errors=[x['actualConsoleErrorCount'] for x in statuses])


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True);parser.add_argument('--demo',type=Path,required=True)
    args=parser.parse_args();print(json.dumps(run(args.root.resolve(),args.demo.resolve())))
