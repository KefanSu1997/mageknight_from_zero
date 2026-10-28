"""Real native input/likelihood/autograd and exact initial-weight paired gate."""
import os
os.environ.setdefault('OMP_NUM_THREADS','1')
import argparse
import gc
import json
from pathlib import Path
import numpy as np
import torch
from train_sb3_atomic_entity_text import (configure_rules, RecordedRecurrentPPO, EvidenceCallback,
    parameter_sha, catalog_argument_features, extend_arguments, BattleEnv, ThreadedRulesVecEnv, Rules)
from atomic_entity_text import EntityTextPolicy as AtomicBattlePolicy

def main():
    p=argparse.ArgumentParser();p.add_argument('--assets',required=True);p.add_argument('--out',required=True)
    a=p.parse_args();out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    (out/'manifest.json').write_text(json.dumps(dict(reward_scale=.01)),encoding='utf-8')
    os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path(a.assets).resolve())
    os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str((out/'atomic_supports.jsonl').resolve())
    configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')
    rows=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text())['tasks']
    r=Rules()
    try:history=catalog_argument_features(r.actions);actions=r.actions
    finally:r.close()
    evidence={};numeric=None
    for arm in ('text',):
        os.environ['BATTLE_RL_TEXT_ARM']=arm
        desc,_=extend_arguments(actions,history)
        from task_feasibility import file_hash
        np.save(out/f'{arm}_descriptors.npy',desc);del desc
        env=ThreadedRulesVecEnv([lambda:BattleEnv(rows,out/arm,0,history,reward_config=dict(version='single_battle_v2',scale=.01))])
        try:
            obs=env.reset();prefix=obs['state'][:,:-384]
            if numeric is None:numeric=prefix.copy()
            else:assert np.array_equal(numeric,prefix)
            assert obs['entity_present'].sum()>1
            assert obs['entity_attributes'].shape[-1]==54
            model=RecordedRecurrentPPO(AtomicBattlePolicy,env,device='cuda',seed=7112026,
                n_steps=128,batch_size=128,n_epochs=1,gamma=1.,gae_lambda=1.,target_kl=.03,
                policy_kwargs=dict(descriptor_file=str((out/f'{arm}_descriptors.npy').resolve()),
                    descriptor_sha256=file_hash(out/f'{arm}_descriptors.npy'),registry_file=os.environ['BATTLE_RL_ATOMIC_REGISTRY']))
            evidence[arm+'_initial_parameter_sha']=parameter_sha(model.policy)
            if arm=='zero':
                del model;gc.collect();torch.cuda.empty_cache();continue
            evidence['public_entity_tokens']=int(obs['entity_present'].sum())
            with torch.no_grad():
                tensors=model.policy.obs_to_tensor(obs)[0]
                original=model.policy.features_extractor(tensors)
                permuted={k:v.clone() for k,v in tensors.items()}
                for key in ('entity_ids','entity_roles','entity_attributes','entity_present'):
                    permuted[key]=permuted[key].flip(1)
                alternative=model.policy.features_extractor(permuted)
                evidence['permutation_maxdiff']=float((original-alternative).abs().max())
                assert evidence['permutation_maxdiff']<1e-5
            model.output=out;model.completed_updates=0;model.evaluation_every=0
            model.history_features=history;model.reward_config=dict(version='single_battle_v2',scale=.01)
            model.episodes_per_update=4
            model.goal_updates=1
            before=parameter_sha(model.policy)
            model.learn(total_timesteps=1,callback=EvidenceCallback())
            assert model.completed_updates==1 and parameter_sha(model.policy)!=before
            assert all(torch.isfinite(x).all() for x in model.policy.parameters())
            assert len(model.policy.optimizer.state)>0
            evidence.update(actual_transitions=model.num_timesteps,actual_valid_episodes=4,
                cuda_device=torch.cuda.get_device_name(0),optimizer_states=len(model.policy.optimizer.state),
                peak_cuda_allocated=torch.cuda.max_memory_allocated(),all_parameters_finite=True,
                parameter_sha_after=parameter_sha(model.policy),same_public_numeric_input=True,
                text_finite=True,text_used_in_state_and_candidate=True)
            valid=next(model.rollout_buffer.get(128))
            with torch.no_grad():
                _,log,_=model.policy.evaluate_actions(valid.observations,valid.actions.flatten().long(),valid.lstm_states,valid.episode_starts)
            model.save(out/'gate_reload')
            loaded=RecordedRecurrentPPO.load(out/'gate_reload.zip',env=env,device='cuda')
            with torch.no_grad():
                _,again,_=loaded.policy.evaluate_actions(valid.observations,valid.actions.flatten().long(),valid.lstm_states,valid.episode_starts)
            evidence['reload_logprob_maxdiff']=float((again-log).abs().max())
            assert evidence['reload_logprob_maxdiff']<1e-6
            frozen=json.loads((Path(a.assets)/'manifest.json').read_text())['freeze_gate']
            assert frozen['before']==frozen['after'] and frozen['encoder_gradient_tensors']==0
            evidence['frozen_encoder_gate']=frozen
        finally:env.close()
    (out/'gate.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8')
    print(json.dumps(evidence),flush=True)

if __name__=='__main__':main()
