"""Strict completed-episode continuation with opt-in lossless transport changes."""
import argparse, gc, json, os, shutil
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from atomic_entity_text import BattleEnv
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
from task_feasibility import file_hash
from original_data_signature import data_signature

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--parent',required=True);ap.add_argument('--out',required=True);ap.add_argument('--goal',type=int,default=300)
    args=ap.parse_args();parent=Path(args.parent);output=Path(args.out)
    gate_root=Path('H:/MageKnight_RL_Runs/20261013_eval200_recovery_gate')
    approval=json.loads((gate_root/'deployment_gate.json').read_text())
    assert approval['passed']
    for name,sha in approval['implementation_sha'].items():
        assert file_hash(Path(__file__).parent/name)==sha,('untested optimization',name)
    for name,sha in approval['parent_sha'].items():
        assert file_hash(parent/name)==sha,('changed parent',name)
    old=json.loads((parent/'manifest.json').read_text());config=old['config']
    for name,sha in old['implementation_sha'].items():
        assert file_hash(Path(base.__file__).parent/name)==sha,('source changed',name)
    assert file_hash(config['dll'])==old['rules_sha']
    assert file_hash(config['train_pool'])==old['train_sha']
    assert file_hash(config['eval_pool'])==old['eval_sha']
    assert data_signature()['sha256']==old['data_sha']
    os.environ['BATTLE_RL_TEXT_ASSETS']=config['text_assets'];os.environ['BATTLE_RL_TEXT_ARM']='text'
    output.mkdir(parents=True,exist_ok=False)
    shutil.copy2(parent/'atomic_supports.jsonl',output/'atomic_supports.jsonl')
    shutil.copy2(parent/'action_descriptors.npy',output/'action_descriptors.npy')
    os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str((output/'atomic_supports.jsonl').resolve())
    base.configure_rules(config['dll']);torch.set_num_threads(1)
    install()
    from entity_fast_evaluation import frozen_evaluation
    base.frozen_evaluation=frozen_evaluation
    train=json.loads(Path(config['train_pool']).read_text());rows=train['tasks']+train.get('unknown_tasks',[])
    evaluation=json.loads(Path(config['eval_pool']).read_text())['tasks']
    rules=Rules();history=catalog_argument_features(rules.actions);rules.close()
    state=torch.load(parent/'continuation.pt',weights_only=False,map_location='cpu')
    completed=state['completed_updates']
    assert np.all(state['episode_starts'])
    assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
    assert len(state['environments'])==32
    assert all(s['closed_episode'] for s in state['environments'])
    def create(i):return BattleEnv(rows,output/'episodes',i,history,reward_config={'version':old['reward_version'],'scale':old['reward_scale']})
    native=[create(0)]
    with ThreadPoolExecutor(max_workers=4) as pool:native.extend(pool.map(create,range(1,32)))
    env=ThreadedRulesVecEnv([lambda e=e:e for e in native])
    try:
        model=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
        assert model.completed_updates==completed
        assert model.gae_lambda==1 and model.gamma==1 and model.batch_size==128
        original_sha=base.parameter_sha(model.policy)
        assert original_sha==approval['parameter_sha'] and completed==200
        assert all(torch.isfinite(v).all() for s in model.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
        model.policy.registry_file=str(output/'atomic_supports.jsonl')
        model.policy_kwargs['registry_file']=model.policy.registry_file
        for target,snapshot in zip(env.envs,state['environments']):
            target.rng.bit_generator.state=snapshot['rng'];target.results=snapshot['results']
            target.episode=snapshot['episode'];target.total_steps=snapshot['total_steps']
            target.quarantined_tasks.update(snapshot.get('quarantined_tasks',[]))
        # Atomic snapshots did not always serialize this class-level set.
        for file in ('audit_quarantine.jsonl','inherited_quarantine.jsonl'):
            if (parent/file).exists():
                for line in (parent/file).read_text().splitlines():BattleEnv.quarantined_tasks.add(json.loads(line)['task_id'])
        model._last_lstm_states=type(model._last_lstm_states)(*(tuple(t.to('cuda') for t in pair) for pair in state['lstm']))
        model._last_obs=state['last_obs'];model._last_episode_starts=state['episode_starts']
        for filename in ('metrics.jsonl','performance.jsonl','evaluation_metrics.jsonl','audit_quarantine.jsonl','episode_batches.jsonl'):
            if (parent/filename).exists():shutil.copy2(parent/filename,output/filename)
        manifest=dict(old);manifest['transport_optimization']={n:file_hash(Path(__file__).parent/n) for n in ('atomic_transport_fast.py','parallel_atomic_eval_fast.py','entity_fast_evaluation.py',Path(__file__).name)}
        manifest['config']=dict(config,out=str(output),updates=args.goal)
        manifest['continuation_parent']=str(parent);manifest['initialization']='strict complete-episode continuation'
        (output/'manifest.json').write_text(json.dumps(manifest,indent=2))
        model.output=output;model.goal_updates=args.goal;model.evaluation_rows=evaluation;model.history_features=history
        model.evaluation_every=20;model.reward_config={'version':old['reward_version'],'scale':old['reward_scale']}
        shutil.copy2(parent/'initial_checkpoint.zip',output/'initial_checkpoint.zip')
        shutil.copy2(parent/'checkpoint.zip',output/'checkpoint.zip')
        shutil.copy2(parent/'continuation.pt',output/'continuation.pt')
        (output/'continuation_loaded.json').write_text(json.dumps(dict(completed_updates=completed,parameter_sha=original_sha,optimizer_updates=model._n_updates,optimizer_state_entries=len(model.policy.optimizer.state),support_sha=file_hash(output/'atomic_supports.jsonl'),parent_checkpoint_sha=file_hash(parent/'checkpoint.zip'),parent_rng_sha=file_hash(parent/'continuation.pt'),closed_workers=32,lstm_zero=True),indent=2))
        base.restore_rng(state['rng']);del state,snapshot;gc.collect()
        evaluated={json.loads(x)['update'] for x in (output/'evaluation_metrics.jsonl').read_text().splitlines()}
        if completed%20==0 and completed not in evaluated:base.frozen_evaluation(model,evaluation,history)
        evaluations=[json.loads(x) for x in (output/'evaluation_metrics.jsonl').read_text().splitlines()]
        current=next(x for x in evaluations if x['update']==200)
        best=max(x['wins'] for x in evaluations if x['update']<200 and x['episodes']==1000)
        assert current['episodes']==1000 and current['frozen_parameters'] and current['training_rng_restored']
        decision=dict(update=200,wins=current['wins'],previous_best=best,status='new_high_continue_current' if current['wins']>best else 'stopped_no_new_high_prepare_hierarchical')
        (output.parent/'eval200_switch_status.json').write_text(json.dumps(decision,indent=2))
        if current['wins']<=best:
            model.save(output/'behavior_0200_final')
            (output/'stopped_for_jev.json').write_text(json.dumps(decision,indent=2))
            return
        while model.completed_updates<args.goal:
            model.learn(total_timesteps=1,reset_num_timesteps=False,callback=base.EvidenceCallback())
        model.save(output/f'behavior_{model.completed_updates:04d}_final')
        (output/'complete.json').write_text(json.dumps(dict(updates=model.completed_updates,timesteps=model.num_timesteps,parameter_sha=base.parameter_sha(model.policy))))
    finally:env.close()

if __name__=='__main__':main()
