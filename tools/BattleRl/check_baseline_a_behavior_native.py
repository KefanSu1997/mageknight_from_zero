"""Real native transitions and recurrent behavior archive; controlled text reader."""
import json,os,subprocess,tempfile
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from baseline_a_scenario_inputs import ScenarioInputs
from baseline_a_scenario_runtime import ScenarioRuntime
from baseline_a_scenario_policy import BaselineAScenarioPolicy
from baseline_a_scenario_behavior import ScenarioBehavior
from baseline_a_scenario_text import ScenarioText
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets


def run():
    torch.set_num_threads(2);torch.manual_seed(7112026)
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    assets=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    old=Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets');ids=json.loads((old/'manifest.json').read_text())['entity_ids']
    vectors=np.concatenate((np.load(old/'entity_vectors.npy'),np.zeros((1,384),np.float32)))
    inputs=ScenarioInputs('.',schema,assets,ids)
    policy=BaselineAScenarioPolicy(inputs.space,gym.spaces.Discrete(2**24-1),lambda _:1e-4,entity_vectors=vectors)
    tasks=json.loads(Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/family_export_gate_setups.json').read_text(encoding='utf-8-sig'))[0]['tasks'][:3]
    class Reader:
        def encode_many(self,documents):
            self.last_lengths=[1]*len(documents)
            return [np.zeros((len(d['options'])+1,384),np.float32) for d in documents]
    processes=[]
    def call(p,request):
        p.stdin.write(json.dumps(request)+'\n');p.stdin.flush();reply=json.loads(p.stdout.readline())
        if not reply.get('ok'):raise ValueError(reply)
        return reply['result']
    with tempfile.TemporaryDirectory() as tmp:
        runtime=ScenarioRuntime(tmp,{'gate':'controlled-zero-context-real-native-v1'});runtime.reader=Reader()
        behavior=ScenarioBehavior(policy,inputs,runtime,ScenarioText(schema,assets));contexts=[];states=[]
        try:
            for i,task in enumerate(tasks):
                env=dict(os.environ,SOLO_BASELINE_A_OBSERVATION='1')
                p=subprocess.Popen(['dotnet',str(root/'observation_transport_bin/SoloConquestRl.dll'),str(Path('.').resolve())],
                    stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',env=env)
                processes.append(p);states.append(call(p,dict(op='reset',setup=task['setup'])));contexts.append(behavior.begin(7112026+i))
            for step in range(3):
                choices=behavior.select_many(states,contexts);following=[]
                for p,state,c,choice in zip(processes,states,contexts,choices):
                    result=call(p,dict(op='step',index=choice['index'],support_sha256=state['support_sha256'],
                        log_probability=choice['log_probability'],value=choice['value']))
                    behavior.transition(result,c);following.append(result['state'])
                    assert c['rows'][-1]['reward']==result['trajectory']['Transition']['Reward']
                    assert c['history'][-1]==np.float32(c['rows'][-1]['reward'])
                states=following
            for c in contexts:
                snapshot=behavior.snapshot(c);assert len(snapshot['rows'])==3 and snapshot['pending'] is None
                assert [r['episode_start'] for r in snapshot['rows']]==[True,False,False]
                assert torch.count_nonzero(snapshot['rows'][0]['lstm_before'].pi[0])==0
                assert torch.count_nonzero(snapshot['rows'][1]['lstm_before'].pi[0])>0
                # Re-evaluation begins from the saved behavior memory, not from game start.
                for row in snapshot['rows']:
                    obs={k:torch.as_tensor(v[None]) for k,v in row['observation'].items()}
                    with torch.no_grad():
                        value,logp,_=policy.evaluate_actions(obs,torch.tensor([row['action']]),row['lstm_before'],torch.tensor([float(row['episode_start'])]))
                    assert abs(float(logp)-row['log_probability'])<1e-5
                    assert abs(float(value)-row['value'])<1e-5
            return dict(passed=True,environments=3,actual_native_actions=9,behavior_replay_verified=True,
                rewards_and_history_exact=True,optimizer_steps=0,
                scope='Actual short native prefixes and saved-state PPO replay with zero controlled marker vectors; no full-game/CUDA/real-text acceptance')
        finally:
            for p in processes:
                p.stdin.close()
                try:p.wait(timeout=10)
                except subprocess.TimeoutExpired:p.terminate();p.wait(timeout=10)
                p.stdout.close();p.stderr.close()
            runtime.close()

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/scenario_behavior_native_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
