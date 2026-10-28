"""Fresh joint-context option policy; original rules/reward/complete-episode PPO."""
import argparse,json,os,time
from pathlib import Path
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from hierarchical_option_policy import BattleEnv
from root_entropy_conditioned_policy import RootEntropyConditionedPolicy as JointOptionPolicy
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference
from fresh_text_conditioning import fit_fresh_conditioning
from joint_option_text import SNAPSHOT
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
from task_feasibility import file_hash,digest
from original_data_signature import data_signature
from coverage_tasks import combination
import hierarchical_evaluation as evaluation

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--updates',type=int,default=300)
    p.add_argument('--gate',action='store_true');p.add_argument('--workers',type=int,default=32)
    a=p.parse_args();out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    assets='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
    os.environ.update(BATTLE_RL_CONTEXT_STORE=str(out/'contexts'),BATTLE_RL_ATOMIC_REGISTRY=str(out/'atomic_supports.jsonl'),BATTLE_RL_TEXT_ASSETS=assets)
    assert __import__('shutil').disk_usage(out).free>3*1024**3
    base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');torch.set_num_threads(1);install();packed.install()
    base.memory_record=lambda output,stage:None
    train_path=Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json');eval_path=train_path.with_name('eval.json')
    source=json.loads(train_path.read_text());rows=source['tasks']+source.get('unknown_tasks',[]);ev=json.loads(eval_path.read_text())['tasks']
    train_keys=[digest(combination(r['task']['definition'])) for r in rows];eval_keys=[digest(combination(r['task']['definition'])) for r in ev]
    assert len(set(train_keys))==4000 and len(set(eval_keys))==1000 and not set(train_keys)&set(eval_keys)
    r=Rules();history=catalog_argument_features(r.actions);catalog_sha=digest(r.actions);schema=r.info['featureSchema'];r.close()
    dll=os.environ['BATTLE_RL_DLL'];dll_sha=file_hash(dll);data=data_signature()
    for row in rows+ev:
        t=row['task'];assert len(t['definition']['EnemyGroups'])==1 and t['rules_sha256']==dll_sha and t['catalog_sha256']==catalog_sha and t['feature_schema']==schema and row['task_id']==digest(t) and row['data_signature']==data
    if not a.gate:
        ready=Path('remote_repaired_fresh_gate32_retry')
        gate=json.loads((ready/'gate.json').read_text());assert gate['passed'] and gate['reload_all_buffers_equal']
        binding=json.loads((ready/'manifest.json').read_text())['repair_runtime_sha']
        for name,sha in binding.items():assert file_hash(Path('optimization_stage_20261014')/name)==sha,name
    manifest=dict(config=vars(a),architecture='joint public state + locked prefix + legal next choices; frozen contextual marker readout; irreversible parameter tree; leaf log probability is sum of branch log probabilities',
        reward_scale=.01,reward_version=base.SingleBattleReward.version,rules_sha=dll_sha,train_sha=file_hash(train_path),eval_sha=file_hash(eval_path),
        data_sha=data['sha256'],catalog_sha=catalog_sha,encoder_files={p.name:file_hash(p) for p in SNAPSHOT.iterdir() if p.is_file()},
        implementation_sha={n:file_hash(Path('tools/BattleRl')/n) for n in ['joint_option_text.py','joint_option_policy.py','joint_chunked_attention.py','hierarchical_option_policy.py','parallel_hierarchical_eval.py','hierarchical_evaluation.py','train_hierarchical_option_ppo.py','atomic_transport_fast.py','parallel_atomic_eval_fast.py','entity_fast_evaluation.py','train_sb3_atomic_entity_text.py','complete_episode_collector.py']},
        frozen_encoder=True,encoder_precision='bfloat16; trainable PPO head float32',context_limit=8192,pretrained_context=8192,long_context='no positional extrapolation; overlength is explicit error',no_truncation=True,no_topk=True,gate_only=a.gate,scope='Aligned contextual-option paradigm; not official Jev/RLCD reproduction; no calibrated success head')
    manifest['fresh']=True
    manifest['repair_runtime_sha']={n:file_hash(Path('optimization_stage_20261014')/n) for n in ['train_repaired_fresh.py','fresh_text_conditioning.py','root_entropy_conditioned_policy.py','conditioned_context_policy.py','context_state_policy.py','entropy_token_value_policy.py','batched_token_value_policy.py']}
    manifest['initialization']='Random trainable parameters and empty Adam; fresh worker/global RNG; no checkpoint load. Training-only frozen contextual vectors used for normalization statistics.'
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
        if a.gate:model.episodes_per_update=32
        assert model.num_timesteps==0 and len(model.policy.optimizer.state)==0
        fit_fresh_conditioning(model,out)
        assert len(model.policy.optimizer.state)==0
        initial_state=model.policy.state_dict()
        (out/'fresh_initialization.json').write_text(json.dumps(dict(completed_updates=0,timesteps=0,optimizer_states=0,parameter_sha=base.parameter_sha(model.policy),trainable_parameters=sum(p.numel() for p in model.policy.parameters()),workers=a.workers,previous_checkpoint_loaded=False,encoder_frozen=True)))
        model.save(out/'initial_checkpoint');torch.save(base.rng_state(),out/'initial_rng.pt')
        before=base.parameter_sha(model.policy)
        if not a.gate:base.frozen_evaluation(model,ev,history)
        started=time.perf_counter()
        while model.completed_updates<a.updates:
            assert __import__('shutil').disk_usage(out).free>3*1024**3,'Low disk reserve at complete boundary'
            model.learn(total_timesteps=1,reset_num_timesteps=model.completed_updates==0,callback=base.EvidenceCallback())
        if a.gate:
            finite=all(torch.isfinite(v).all() for s in model.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
            assert finite and before!=base.parameter_sha(model.policy)
            restored=base.RecordedRecurrentPPO.load(out/'checkpoint.zip',device='cuda')
            assert base.parameter_sha(restored.policy)==base.parameter_sha(model.policy)
            assert all(torch.equal(v,restored.policy.state_dict()[n]) for n,v in model.policy.state_dict().items())
            last=json.loads((out/'metrics.jsonl').read_text().splitlines()[-1]);assert last['episodes']==32 and last['audit_failed_episodes']==0
            from sb3_contrib.common.recurrent.type_aliases import RNNStates
            obs={k:torch.as_tensor(v[0:1],device='cuda') for k,v in model.rollout_buffer.observations.items()}
            zeros=lambda:torch.zeros((1,1,256),device='cuda')
            memory=RNNStates((zeros(),zeros()),(zeros(),zeros()))
            with torch.no_grad():
                original=model.policy.forward(obs,memory,torch.ones(1,device='cuda'),deterministic=True)
                reloaded=restored.policy.forward(obs,memory,torch.ones(1,device='cuda'),deterministic=True)
                assert torch.equal(original[0],reloaded[0])
                log_difference=float((original[2]-reloaded[2]).abs().max());assert log_difference==0
                dist,_=model.policy.get_distribution(obs,memory.pi,torch.ones(1,device='cuda'))
                from hierarchical_option_policy import tree
                context=int(obs['context_id'].item());count=tree(str(out/'contexts'),context)[0].leaves
                probabilities=torch.stack([dist.log_prob(torch.tensor([i],device='cuda')).exp()[0] for i in range(count)])
                probability_sum=float(probabilities.sum());assert abs(probability_sum-1)<1e-5 and bool((probabilities>0).all())
            from atomic_action_registry import registry
            documents=[json.loads(s) for s in (out/'contexts/contexts.jsonl').read_text(encoding='utf-8').splitlines()]
            assert all(d['sequences']==registry(str(out/'atomic_supports.jsonl')).get(d['support_id']) for d in documents)
            import parallel_hierarchical_eval as pe
            pe.BattleEnv=BattleEnv
            model.policy.set_training_mode(False)
            frozen_sha=base.parameter_sha(model.policy)
            checks=[]
            for workers in (1,3):
                folder=out/f'eval_gate_workers{workers}';folder.mkdir()
                checks.append(pe.collect(model,ev[:4],history,folder,workers=workers))
            assert checks[0]==checks[1] and base.parameter_sha(model.policy)==frozen_sha
            (out/'gate.json').write_text(json.dumps(dict(passed=True,updates=model.completed_updates,seconds=time.perf_counter()-started,
                real_games=32,reload_all_buffers_equal=True,optimizer_states=len(model.policy.optimizer.state),finite=finite,parameters_changed=True,reload_sha_equal=True,
                reload_log_probability_difference=log_difference,full_leaf_probability_sum=probability_sum,all_leaf_probabilities_positive=True,
                context_supports_equal=len(documents),parallel_eval_equal=True,eval_gate_tasks=4,peak_cuda_allocated=torch.cuda.max_memory_allocated()),indent=2))
        else:model.save(out/f'behavior_{model.completed_updates:04d}_final')
        (out/'complete.json').write_text(json.dumps(dict(updates=model.completed_updates,timesteps=model.num_timesteps)))
    finally:env.close()
original_train=base.RecordedRecurrentPPO.train
def entropy_train(model):
    bind_reference(model)
    try:return original_train(model)
    finally:model.policy._entropy_reference=None
base.RecordedRecurrentPPO.train=entropy_train
if __name__=='__main__':main()

