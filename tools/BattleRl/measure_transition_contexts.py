"""Fresh real UI reproduction of frequent transitions with explicit context."""
import argparse
import json
from pathlib import Path
from measure_ui_fluency import Trial, intent_metrics
from analyze_ui_transition_costs import panel, native, edges_from_exchanges
from structured_ui_ppo import state, write_json


def finish(trial, expected):
    case=trial.current
    case['verification']=expected
    case['completed']=all(expected.values())
    case['metrics']=intent_metrics(case)
    case['metrics']['panel_transitions']=sum(panel(x['actual']['before'])!=panel(x['actual']['observation']) for x in case['steps'])
    trial.save();trial.current=None
    assert case['completed'],expected


def play(trial,name):
    trial.click(name)
    before=state(trial.observation)
    trial.click('Btn_Play',screenshot=True)
    after=state(trial.observation)
    assert int(after['played'])==int(before['played'])+1
    assert int(after['rejections'])==int(before['rejections'])


def source(trial,color):
    trial.click('Btn_Menu');trial.click('Btn_JourneyTools')
    cn={'Red':'红','Blue':'蓝','White':'白'}[color]
    dice=[x for x in trial.observation['controls'] if x['clickable'] and x['name'].startswith('Btn_Source_')]
    die=next((x for x in dice if cn in x['text']),None)
    if die is None:die=next(x for x in dice if '金' in x['text'])
    trial.click(die['name']);trial.click('Btn_UseSource')
    acquired=native(trial.observation)
    assert acquired['sourceUsedThisTurn'] and sum(acquired['tokens'].values())==1
    trial.click('Btn_CloseTools')


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--attempts',type=int,default=6);p.add_argument('--matched',type=int,default=2)
    args=p.parse_args();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    matched=0;results=[]
    contracts={'basic_card_007':('White','Ranged'),'basic_card_016':('Blue','Block'),
               'basic_card_020':('Blue','Block'),'basic_card_021':('Red','Melee')}
    for attempt in range(args.attempts):
        trial=Trial(out/f'trial_{attempt:02d}')
        try:
            available=state(trial.observation)['source.available'].split(',')
            selected=None
            for c in trial.observation['controls']:
                if not c['clickable']:continue
                for cid,(color,phase) in contracts.items():
                    if c['name'].startswith('Btn_Card_'+cid+'_') and (color in available or 'Gold' in available):
                        selected=(c['name'],cid,color,phase);break
                if selected:break
            if selected is None:
                results.append({'attempt':attempt,'status':'no_matching_fixture','source':available})
                write_json(trial.path/'fixture_gap.json',results[-1]);continue
            name,cid,color,phase=selected
            for _ in range({'Ranged':0,'Block':1,'Melee':3}[phase]):trial.click('Btn_Next')
            start=native(trial.observation);faces=state(trial.observation)['source.faces']
            outcomes=[]
            for prepared in (True,False):
                if not prepared:
                    trial.click('Btn_ResetPhase')
                    assert native(trial.observation)==start,'Actual checkpoint reset differs'
                    assert state(trial.observation)['source.faces']==faces
                trial.begin('source_to_play_preselected' if prepared else 'source_to_play_unselected',8)
                if prepared:trial.click(name);trial.click('Btn_Choice_strong')
                source(trial,color)
                if not prepared:trial.click(name);trial.click('Btn_Choice_strong')
                mode=state(trial.observation)['ui.cardMode']
                before=state(trial.observation)
                trial.click('Btn_Play',screenshot=True)
                after=state(trial.observation);outcomes.append(native(trial.observation))
                finish(trial,dict(mode_strong=mode=='strong',played=int(after['played'])==int(before['played'])+1,
                    paid=sum(outcomes[-1]['tokens'].values())==0,
                    no_reject=after['rejections']==before['rejections'],
                    card_consumed=start['hand'].count(cid)-outcomes[-1]['hand'].count(cid)==1))
            assert outcomes[0]==outcomes[1],'Same source/card native outcome must match'
            matched+=1
            results.append(dict(attempt=attempt,status='paired_source_completed',card=cid,phase=phase,
                required=color,source=available,native_outcomes_equal=True))
            write_json(trial.path/'paired_verification.json',results[-1])
            # Additional high-frequency play->play on the same real hand, after
            # moving to Melee normally. Simple native base attack choices only.
            trial.click('Btn_ResetPhase')
            while state(trial.observation)['phase']!='Melee':trial.click('Btn_Next')
            attack_ids=('basic_card_016','basic_card_020','basic_card_021')
            count=sum(native(trial.observation)['hand'].count(cid) for cid in attack_ids)
            if count>=2:
                trial.begin('play_to_play_base_attack',4)
                for _ in range(2):
                    c=next(c for c in trial.observation['controls'] if c['clickable'] and any(c['name'].startswith('Btn_Card_'+cid+'_') for cid in attack_ids))
                    play(trial,c['name'])
                finish(trial,dict(two_commits=True))
            else:write_json(trial.path/'base_attack_gap.json',dict(available=count,scope='paired base attack needs two actual cards'))
        except Exception as error:
            write_json(trial.path/'failure.json',dict(error=repr(error),actual_state=state(trial.observation)))
            raise
        finally:trial.demo.close()
        write_json(out/'summary.json',dict(results=results,matched=matched,scope='fresh scripted UI reproduction; no policy/human claim'))
        if matched>=args.matched:break
    write_json(out/'summary.json',dict(results=results,matched=matched,scope='fresh scripted UI reproduction; no policy/human claim'))
    assert matched>=args.matched,'Insufficient matching actual fixtures; gaps retained'
    print(json.dumps(results,ensure_ascii=False),flush=True)


if __name__=='__main__':main()
