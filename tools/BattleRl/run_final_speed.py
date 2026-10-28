"""Checkpoint-identical sampling benchmark and gated CPU-speed continuation."""
import os,sys,json,time,hashlib,shutil,gc,argparse,cProfile,pstats,io
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='8')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
p=argparse.ArgumentParser();p.add_argument('--mode',choices=['old','fast','formal'],required=True);p.add_argument('--out',required=True);a=p.parse_args()
import torch,numpy as np,psutil
import train_sb3_atomic_entity_text as b
from final_token_policy import BattleEnv
from final_token_runtime import install_journal
from atomic_transport_fast import install
from entropy_token_value_policy import bind_reference
from sb3_battle_vec import ThreadedRulesVecEnv
from train_ppo import Rules
from action_argument_features import catalog_argument_features
from concurrent.futures import ThreadPoolExecutor
parent=Path('remote_final_semantic300');out=Path(a.out);stage=Path('optimization_stage_20261014')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for proc in psutil.process_iter(['pid','cmdline']):
    if proc.pid!=os.getpid() and any(Path(x).name in ('run_final_semantic.py','run_final_speed.py') for x in (proc.info['cmdline'] or [])[1:]):raise RuntimeError('Active trainer/benchmark '+str(proc.pid))
old=json.loads((parent/'manifest.json').read_text());pause=json.loads((parent/'speed_pause.json').read_text())
for n,h in old['final_token_sources'].items():assert sha(stage/n)==h
for n in ('checkpoint.zip','continuation.pt'):assert sha(parent/n)==pause['files'][n]
if a.mode=='formal':
    speed=json.loads((stage/'final_sampling_speed_gate.json').read_text());assert speed['passed']
    assert speed['candidate_sha']==sha(stage/'final_cpu_fast.py')
out.mkdir(exist_ok=False);(out/'contexts').mkdir()
shutil.copyfile(parent/'atomic_supports.jsonl',out/'atomic_supports.jsonl')
os.environ.update(BATTLE_RL_CONTEXT_STORE=str((out/'contexts').resolve()),BATTLE_RL_ATOMIC_REGISTRY=str((out/'atomic_supports.jsonl').resolve()),BATTLE_RL_TEXT_ASSETS=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve()))
torch.set_num_threads(1);b.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();install_journal();b.memory_record=lambda *args:None
assert sha(os.environ['BATTLE_RL_DLL'])==old['rules_sha']
if a.mode!='old':
    import final_cpu_fast
    gate=json.loads((stage/'final_cpu_fast_gate.json').read_text());assert gate['passed'] and gate['source_sha']==sha(stage/'final_cpu_fast.py')
    final_cpu_fast.install()
data=Path('H:/MageKnight_RL_Runs/atomic_dataset');assert sha(data/'train.json')==old['train_sha'] and sha(data/'eval.json')==old['eval_sha']
src=json.loads((data/'train.json').read_text());rows=src['tasks']+src.get('unknown_tasks',[])
state=torch.load(parent/'continuation.pt',map_location='cpu',weights_only=False);completed=state['completed_updates']
assert completed==pause['completed'] and all(s['closed_episode'] for s in state['environments'])
assert np.count_nonzero(state['last_obs']['context_id'])==0
r=Rules()
try:history=catalog_argument_features(r.actions)
finally:r.close()
env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':b.SingleBattleReward.version,'scale':.01}) for i in range(32)])
def status(message):
    (out/'status.json').write_text(json.dumps(dict(pid=os.getpid(),created=psutil.Process().create_time(),time=time.time(),message=message)))
b.render=lambda output,message:status(message)
try:
    status('restoring')
    m=b.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
    assert m.completed_updates==completed and len(m.policy.optimizer.state)==65
    before=b.parameter_sha(m.policy)
    m.policy.context_path=os.environ['BATTLE_RL_CONTEXT_STORE'];m.policy.registry_file=os.environ['BATTLE_RL_ATOMIC_REGISTRY']
    m.policy_kwargs.update(context_path=m.policy.context_path,registry_file=m.policy.registry_file)
    def restore(i):
        e=env.envs[i];s=state['environments'][i];e.restore(s)
        assert e.current==s['current'] and e.results==s['results'] and e.rng.bit_generator.state==s['rng']
        for k,v in e._observation().items():np.testing.assert_array_equal(v,state['last_obs'][k][i])
    with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(restore,range(32)))
    m._last_lstm_states=type(state['lstm'])(*(tuple(t.to('cuda') for t in pair) for pair in state['lstm']))
    m._last_obs=state['last_obs'];m._last_episode_starts=state['episode_starts']
    m.output=out;m.goal_updates=300;m.history_features=history;m.evaluation_every=0;m.evaluation_rows=[];m.episodes_per_update=128 if a.mode=='formal' else 64
    m.reward_config={'version':b.SingleBattleReward.version,'scale':.01}
    binding=dict(parent=str(parent),completed_updates=completed,parameter_sha=before,adam_states=65,native_workers_restored=32,parent_sha={n:sha(parent/n) for n in ('checkpoint.zip','continuation.pt','manifest.json')},candidate_sha=sha(stage/'final_cpu_fast.py'),mode=a.mode)
    (out/'continuation_loaded.json').write_text(json.dumps(binding,indent=2))
    manifest=dict(old);manifest.update(speed_continuation=binding,evaluation_enabled=False)
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
    if a.mode=='formal':
        for name in ('metrics.jsonl','performance.jsonl'):
            records=[json.loads(x) for x in (parent/name).read_text().splitlines()]
            records=[r for r in records if r['update']<=completed] if name=='metrics.jsonl' else [r for r in records if r['update']<completed]
            (out/name).write_text(''.join(json.dumps(r)+'\n' for r in records))
        original=b.RecordedRecurrentPPO.train
        def train(self):
            bind_reference(self)
            try:return original(self)
            finally:self.policy._entropy_reference=None
        b.RecordedRecurrentPPO.train=train
    else:m.train=lambda:None
    b.restore_rng(state['rng']);del state;gc.collect();status('sampling')
    if a.mode=='formal':
        while m.completed_updates<300:
            assert shutil.disk_usage(out).free>3*1024**3
            m.learn(total_timesteps=1,reset_num_timesteps=False,callback=b.EvidenceCallback())
    else:
        profile=cProfile.Profile();start=time.perf_counter();profile.enable()
        m.learn(total_timesteps=1,reset_num_timesteps=False,callback=b.EvidenceCallback())
        profile.disable();elapsed=time.perf_counter()-start
        assert b.parameter_sha(m.policy)==before
        buf=m.rollout_buffer
        hashes={n:hashlib.sha256(np.asarray(getattr(buf,n)).tobytes()).hexdigest() for n in ('actions','rewards','log_probs','values','returns')}
        stream=io.StringIO();pstats.Stats(profile,stream=stream).sort_stats('cumulative').print_stats(35)
        result=dict(passed=True,mode=a.mode,episodes=64,steps=int(buf.buffer_size),seconds=elapsed,collection_seconds=m.collection_seconds,parameter_sha=before,arrays=hashes,profile=stream.getvalue())
        (out/'benchmark.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
    status('complete')
finally:env.close()
