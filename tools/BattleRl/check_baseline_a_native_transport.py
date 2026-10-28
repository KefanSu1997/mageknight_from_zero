"""Actual paired native protocol projection; no optimizer/model execution."""
import json,os,subprocess
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')

def run():
    setups=json.loads(Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/family_export_gate_setups.json').read_text(encoding='utf-8-sig'))[0]['tasks']
    processes=[]
    try:
        for enabled in (False,True):
            env=dict(os.environ);env['SOLO_BASELINE_A_OBSERVATION']='1' if enabled else '0'
            processes.append(subprocess.Popen(['dotnet',str(OUT/'observation_transport_bin/SoloConquestRl.dll'),str(ROOT)],
                stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',env=env))
        def call(p,request):
            p.stdin.write(json.dumps(request)+'\n');p.stdin.flush()
            line=p.stdout.readline()
            if not line:raise RuntimeError('Native exit '+p.stderr.read())
            reply=json.loads(line)
            if not reply.get('ok'):raise RuntimeError(reply)
            return reply['result']
        count=0;features=[];projected=[]
        for task in setups:
            states=[call(p,dict(op='reset',setup=task['setup'])) for p in processes]
            for step in range(4):
                a,b=states
                for key in ('observation','commands','support_sha256','support_payload_json'):
                    assert a[key]==b[key],key
                assert 'baseline_a_features' not in a
                f=np.asarray(b['baseline_a_features']);assert f.shape==(6314,) and np.isfinite(f).all()
                features.append(f.tolist());count+=1
                projected.append(b)
                before=[call(p,dict(op='export')) for p in processes]
                assert before[0]['current_state_sha256']==before[1]['current_state_sha256']
                if step<3:
                    replies=[call(p,dict(op='step',index=0,support_sha256=s['support_sha256'])) for p,s in zip(processes,states)]
                    semantic=lambda r:{k:v for k,v in r['trajectory'].items() if k not in ('SupportSeconds','ExecuteAndObserveSeconds')}
                    assert semantic(replies[0])==semantic(replies[1])
                    states=[r['state'] for r in replies]
        (OUT/'native_observation_transport_states.json').write_text(json.dumps(projected,ensure_ascii=False),encoding='utf-8')
        return dict(passed=True,heroes=len(setups),paired_states=count,features_width=6314,
                    original_public_state_support_and_transition_exact=True,private_state_digest_exact=True,
                    ignored_nonsemantic_fields=['SupportSeconds','ExecuteAndObserveSeconds'],
                    optimizer_steps=0,scope='Seven hero initial + three complete native operations; full game coverage pending')
    finally:
        for p in processes:
            p.stdin.close()
            try:p.wait(timeout=10)
            except subprocess.TimeoutExpired:p.terminate();p.wait(timeout=10)
            p.stdout.close();p.stderr.close()

if __name__=='__main__':
    result=run();(OUT/'native_observation_transport_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
