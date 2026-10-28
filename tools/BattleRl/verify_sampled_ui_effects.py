import argparse
import json
import hashlib
from pathlib import Path
import sqlite3
import zipfile
from structured_ui_ppo import ROOT
from measure_sampled_ui_pairs import game_difference


def main():
    p=argparse.ArgumentParser();p.add_argument('results',type=Path);args=p.parse_args()
    raw=args.results.read_bytes();data=json.loads(raw)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    errors=[];checked=0
    for r in data['samples']:
        if r['status']!='complete':continue
        volume,path,source_sha=db.execute('SELECT volume,path,sha256 FROM episodes WHERE id=?',(r['episode'],)).fetchone()
        assert r['source']==path and r['source_sha256']==source_sha
        with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:source_raw=z.read(path)
        assert hashlib.sha256(source_raw).hexdigest()==source_sha
        source=json.loads(source_raw)
        rows=r['pointer_steps'];n=r['a_clicks']
        assert n>0,'A-to-B distance requires a measured A pointer endpoint'
        for step in rows:
            actual=step['actual']
            if 'controlCenterX' in actual:
                control=next(c for c in actual['before']['controls'] if c['path']==actual['click']['buttonPath'])
                assert abs(control['x']-actual['controlCenterX'])<.001 and abs(control['y']-actual['controlCenterY'])<.001
            if step.get('raycast_probe'):
                probe=step['raycast_probe']
                # JsonUtility expands null nested classes to default-valued
                # objects. Require absence of dispatch evidence, not falsiness.
                unused_click=probe.get('click') or {};unused_timing=probe.get('timing') or {}
                assert probe['success'] and not unused_click.get('buttonPath') and not unused_click.get('success')
                assert not unused_click.get('label') and not unused_click.get('hitObject') and not unused_click.get('beforeState') and not unused_click.get('afterState')
                assert unused_timing.get('handlerMilliseconds',-1)<0 and unused_timing.get('firstRenderedFrameMilliseconds',-1)<0
                assert actual['before']['version']==probe['observation']['version']
                before={x['key']:x['value'] for x in probe['before']['state']}
                after={x['key']:x['value'] for x in probe['observation']['state']}
                assert json.loads(before['audit.costNativeState'])==json.loads(after['audit.costNativeState']),'Read-only probe must preserve gameplay, identities and protocol bookkeeping'
                assert any(p['valid'] and abs(p['x']-actual['click']['clickX'])<.001 and abs(p['y']-actual['click']['clickY'])<.001 for p in probe['raycastProbes'])
        if r['b_clicks']==0:
            original=source['trace'][r['a_step']+1]
            action=original['action']
            fortitude=action['Kind']=='bannerUse' and action['Card']=='items_018' and action['Option'] in (0,1) and not action['Parts']
            if fortitude:
                unit=original['before']['units'][action['Targets']]
                assert unit['bannerEffect']=='items_018' and unit['fortitudeSelected']==(action['Option']==1)
                assert any(c['name']=='Btn_Fortitude' and c['clickable'] for c in r['zero_click_after_b']['controls']),'No-input fortitude intent requires its visible actual checkbox'
            cleanup=action['Kind'] in ('protectionCleanup','roundReturn')
            clear=action['Kind']=='clearDefend'
            if clear:
                assert not any(action.get(k) for k in ('Card','Choice','Input','Targets','Parts','Option'))
                assert not original['before']['defenderSelected']
                actual_values={x['key']:x['value'] for x in r['zero_click_after_b']['state']}
                assert actual_values.get('enemies.defendSelected')==''
                assert not any('/EnemyDefend/' in c['path'] and c['clickable'] for c in r['zero_click_after_b']['controls'])
            if cleanup:
                controls=r['zero_click_after_b']['controls']
                if action['Kind']=='protectionCleanup':
                    assert action['Card']=='items_002' and action['Option'] in (0,1) and not action['Targets'] and not action['Parts']
                    assert original['before']['artifactFlags']['protectionBurned'] and original['before']['artifactFlags']['removeNewWounds']==(action['Option']==1)
                    assert any(c['name']=='Btn_ProtectionCleanup' and c['clickable'] and ('●' in c['text'])==(action['Option']==1) for c in controls)
                else:
                    assert original['before']['roundWindow'] and original['before']['returningBanners'][action['Targets']]==(action['Option']==1)
                    assert any(c['name']=='Btn_BannerReturn_0_'+str(action['Targets']) and c['clickable'] and ('已选取回' in c['text'])==(action['Option']==1) for c in controls)
            assert (action['Kind'] in ('unitDamage','killCrystal') or fortitude or cleanup or clear) and r.get('b_completion_kind')
            assert not game_difference(original['before'],original['after']),'Zero-click result must be an independently proven no-effect planning intent'
        for index,actual in [(r['a_step'],rows[n-1]['actual']['observation']),(r['a_step']+1,r.get('zero_click_after_b',rows[-1]['actual']['observation']))]:
            s={x['key']:x['value'] for x in actual['state']}
            differences=game_difference(source['trace'][index]['after'],json.loads(s['audit.costNativeState']));checked+=1
            if differences:errors.append(dict(episode=r['episode'],step=index,differences=differences))
    result=dict(status='passed' if not errors else 'failed',result_sha256=hashlib.sha256(raw).hexdigest(),native_effect_snapshots=checked,errors=errors)
    destination=args.results.parent/'strict_effect_verification_final.json'
    destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False));db.close()
    if errors:raise SystemExit(1)


if __name__=='__main__':main()
