"""Actual public source->enhanced card route, independent scripted evidence."""
import argparse
import json
from pathlib import Path
from measure_ui_fluency import Trial, intent_metrics
from structured_ui_ppo import ROOT, state, write_json


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--trials',type=int,default=3)
    args=p.parse_args();out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=False)
    cards={c['id']:c for c in json.loads((ROOT/'resources/text_json/basic_card.json').read_text(encoding='utf-8'))}
    colors={'红':'Red','蓝':'Blue','绿':'Green','白':'White'}
    results=[]
    for attempt in range(args.trials):
        trial=Trial(out/f'trial_{attempt:02d}')
        try:
            # Ranged->Block->Damage->Melee, explicit setup outside measured intent.
            for _ in range(3):trial.click('Btn_Next')
            rule=json.loads(state(trial.observation)['rl.sharedRuleState'])
            source=state(trial.observation)['source.available'].split(',')
            # Simple attack/influence/movement cards; no hidden state or input bypass.
            candidates=[c for c in trial.observation['controls'] if c['clickable'] and c['name'].startswith('Btn_Card_')]
            selected=None
            for control in candidates:
                cid=next((cid for cid in cards if control['name'].startswith('Btn_Card_'+cid+'_')),None)
                # These native strong choices have been verified in this Melee
                # fixture. Movement-only and strong-block cards need other phases.
                if cid not in ('basic_card_007','basic_card_021'):continue
                color=colors.get(cards[cid]['required_crystals'])
                if color in source or 'Gold' in source:
                    selected=(control['name'],cid,color);break
            if selected is None:
                write_json(trial.path/'unmatched_fixture.json',{'hand':rule['hand'],'source':source,
                    'scope':'no simple matching fixture; retained and not a UI failure'})
                results.append({'attempt':attempt,'status':'unmatched_fixture'});continue
            name,cid,color=selected
            trial.begin('public_source_enhance_card',8)
            trial.click(name);trial.click('Btn_Choice_strong')
            before_take=state(trial.observation)
            trial.click('Btn_Menu');trial.click('Btn_JourneyTools')
            dice=[c for c in trial.observation['controls'] if c['clickable'] and c['name'].startswith('Btn_Source_')]
            cn={'Red':'红','Blue':'蓝','Green':'绿','White':'白'}[color]
            die=next((c for c in dice if cn in c['text']),None)
            if die is None:die=next(c for c in dice if '金' in c['text'])
            trial.click(die['name']);trial.click('Btn_UseSource');trial.click('Btn_CloseTools')
            after_take=state(trial.observation)
            # Details may contain translated card name instead of id; preserve raw
            # state and compare actual selected card/control, not a text guess.
            selected_controls=[c['name'] for c in trial.observation['controls'] if c['name']==name]
            trial.click('Btn_Play',screenshot=True)
            after=state(trial.observation)
            b=json.loads(before_take['rl.sharedRuleState']);a=json.loads(after['rl.sharedRuleState'])
            moved=b['hand'].count(cid)-a['hand'].count(cid)
            case=trial.current
            case['completed']=moved==1 and int(after['played'])==int(before_take['played'])+1 and int(after.get('rejections',0))==int(before_take.get('rejections',0)) and after_take.get('ui.cardMode')=='strong'
            case['verification']={'hand_id_count_decrease':moved,'played_increment':int(after['played'])-int(before_take['played']),'source_used':a.get('sourceUsedThisTurn'),
                'tokens_before':b.get('tokens'),'tokens_after_take':json.loads(after_take['rl.sharedRuleState']).get('tokens'),
                'tokens_after_play':a.get('tokens'),'mode_after_return':after_take.get('ui.cardMode'),
                'selected_control_present_after_return':selected_controls,'card':cid,'required_color':color}
            case['metrics']=intent_metrics(case)
            def panel(obs):
                names={c['name'] for c in obs['controls'] if c['clickable']}
                return 'JourneyTools' if 'Btn_CloseTools' in names else 'BattleMenu' if 'Btn_CloseBattleMenu' in names else 'Battle'
            case['metrics']['panel_transitions']=sum(panel(r['actual']['before'])!=panel(r['actual']['observation']) for r in case['steps'])
            case['metrics']['distinct_panels']=sorted({panel(r['actual'][side]) for r in case['steps'] for side in ('before','observation')})
            case['source_only_steps']=case['steps'][2:7]
            case['source_only_metrics']=intent_metrics({'steps':case['source_only_steps'],'completed':case['completed'],'reference_clicks':5})
            case['source_only_metrics']['panel_transitions']=case['metrics']['panel_transitions']
            trial.save()
            results.append({'attempt':attempt,'status':'completed' if case['completed'] else 'failed',
                            'metrics':case['metrics'],'source_only_metrics':case['source_only_metrics'],'verification':case['verification']})
            assert case['completed'],case['verification']
        finally:trial.demo.close()
    write_json(out/'summary.json',{'results':results,'scope':'scripted route measurement, not policy or human success'})
    print(json.dumps(results,ensure_ascii=False),flush=True)


if __name__=='__main__':main()
