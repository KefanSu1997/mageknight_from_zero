"""Fresh joint-context option policy; original rules/reward/complete-episode PPO."""
import argparse,json,os,time
from pathlib import Path
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from joint_option_policy import BattleEnv,JointOptionPolicy
from joint_option_text import SNAPSHOT
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
from task_feasibility import file_hash,digest
from original_data_signature import data_signature
from coverage_tasks import combination
import entity_fast_evaluation as evaluation

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--updates',type=int,default=300)
    p.add_argument('--gate',action='store_true');p.add_argument('--workers',type=int,default=32)
    a=p.parse_args();out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    assets='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
    os.environ.update(BATTLE_RL_CONTEXT_STORE=str(out/'contexts'),BATTLE_RL_ATOMIC_REGISTRY=str(out/'atomic_supports.jsonl'),BATTLE_RL_TEXT_ASSETS=assets)
    base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');torch.set_num_threads(1);install()
    train_path=Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json');eval_path=train_path.with_name('eval.json')
    source=json.loads(train_path.read_text());rows=source['tasks']+source.get('unknown_tasks',[]);ev=json.loads(eval_path.read_text())['tasks']
    train_keys=[digest(combination(r['task']['definition'])) for r in rows];eval_keys=[digest(combination(r['task']['definition'])) for r in ev]
    assert len(set(train_keys))==4000 and len(set(eval_keys))==1000 and not set(train_keys)&set(eval_keys)
    r=Rules();history=catalog_argument_features(r.actions);catalog_sha=digest(r.actions);schema=r.info['featureSchema'];r.close()
    dll=os.environ['BATTLE_RL_DLL'];dll_sha=file_hash(dll);data=data_signature()
    for row in rows+ev:
        t=row['task'];assert len(t['definition']['EnemyGroups'])==1 and t['rules_sha256']==dll_sha and t['catalog_sha256']==catalog_sha and t['feature_schema']==schema and row['task_id']==digest(t) and row['data_signature']==data
    if not a.gate:
        gate=json.loads(Path('H:/MageKnight_RL_Runs/20261013_joint_option_prepare/ready.json').read_text())
        assert gate['passed']
        for name,sha in gate['implementation_sha'].items():assert file_hash(Path(__file__).parent/name)==sha,name
    manifest=dict(config=vars(a),architecture='joint public state + printed rules + all complete options; frozen contextual mmBERT marker readout + recurrent PPO head',
        reward_scale=.01,reward_version=base.SingleBattleReward.version,rules_sha=dll_sha,train_sha=file_hash(train_path),eval_sha=file_hash(eval_path),
        data_sha=data['sha256'],catalog_sha=catalog_sha,encoder_files={p.name:file_hash(p) for p in SNAPSHOT.iterdir() if p.is_file()},
        implementation_sha={n:file_hash(Path(__file__).parent/n) for n in ['joint_option_text.py','joint_option_policy.py','joint_chunked_attention.py','train_joint_option_ppo.py','atomic_transport_fast.py','parallel_atomic_eval_fast.py','entity_fast_evaluation.py','train_sb3_atomic_entity_text.py','complete_episode_collector.py']},
        frozen_encoder=True,encoder_precision='bfloat16; trainable PPO head float32',context_limit=32768,pretrained_context=8192,long_context='default RoPE positional extrapolation; no pretrained quality guarantee',no_truncation=True,no_topk=True,gate_only=a.gate,scope='Aligned contextual-option paradigm; not official Jev/RLCD reproduction; no calibrated success head')
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01}) for i in range(a.workers)])
    # Fresh comparison retains the known interface quarantine, not ordinary losses.
    q=Path('H:/MageKnight_RL_Runs/20261013_mmbert_entity_fast180_to300/text/audit_quarantine.jsonl')
    if q.exists():
        errors=[json.loads(s) for s in q.read_text().splitlines()];BattleEnv.quarantined_tasks.update(e['task_id'] for e in errors)
        (out/'inherited_quarantine.json').write_text(json.dumps(errors,indent=2))
    try:
        model=base.RecordedRecurrentPPO(JointOptionPolicy,env,device='cuda',seed=7112026,n_steps=128,batch_size=128,n_epochs=4,
            learning_rate=1e-4,gamma=1.,gae_lambda=1.,clip_range=.2,ent_coef=.01,vf_coef=.5,max_grad_norm=.5,target_kl=.03,
            policy_kwargs={'context_path':str(out/'contexts'),'registry_file':str(out/'atomic_supports.jsonl')},verbose=1)
        model.completed_updates=0;model.output=out;model.goal_updates=a.updates;model.history_features=history
        model.reward_config={'version':base.SingleBattleReward.version,'scale':.01}
        model.evaluation_rows=ev;model.evaluation_every=0 if a.gate else 20
        evaluation.BattleEnv=BattleEnv;base.frozen_evaluation=evaluation.frozen_evaluation
        if a.gate:model.episodes_per_update=4
        model.save(out/'initial_checkpoint');torch.save(base.rng_state(),out/'initial_rng.pt')
        before=base.parameter_sha(model.policy)
        if not a.gate:base.frozen_evaluation(model,ev,history)
        started=time.perf_counter()
        while model.completed_updates<a.updates:model.learn(total_timesteps=1,reset_num_timesteps=model.completed_updates==0,callback=base.EvidenceCallback())
        if a.gate:
            finite=all(torch.isfinite(v).all() for s in model.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
            assert finite and before!=base.parameter_sha(model.policy)
            restored=base.RecordedRecurrentPPO.load(out/'checkpoint.zip',device='cuda')
            assert base.parameter_sha(restored.policy)==base.parameter_sha(model.policy)
            (out/'gate.json').write_text(json.dumps(dict(passed=True,updates=model.completed_updates,seconds=time.perf_counter()-started,
                optimizer_states=len(model.policy.optimizer.state),finite=finite,parameters_changed=True,reload_sha_equal=True),indent=2))
        else:model.save(out/f'behavior_{model.completed_updates:04d}_final')
        (out/'complete.json').write_text(json.dumps(dict(updates=model.completed_updates,timesteps=model.num_timesteps)))
    finally:env.close()
if __name__=='__main__':main()
