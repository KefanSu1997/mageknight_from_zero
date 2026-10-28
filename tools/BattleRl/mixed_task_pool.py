"""Train-only legal unknown admissions, separate from solvability certificates."""
import argparse
import copy
import json
import os
from collections import Counter
from pathlib import Path
import numpy as np
from certified_task_sampler import CertifiedTaskSampler, desired_modes
from coverage_tasks import split
from original_data_signature import data_signature
from task_feasibility import SEQUENCE_GOAL, digest, eligible, file_hash
from recurrent_checkpoint import load_checkpoint, save_checkpoint


class MixedTaskSampler(CertifiedTaskSampler):
    def probabilities(self):
        heroes=Counter()
        for key,count in self.attempts.items(): heroes[self.by_id[key]['hero']]+=count
        weights=np.array([(1+sum(self.events[k]==0 for k in desired_modes(c))/max(1,len(desired_modes(c))))
                          /np.sqrt((1+heroes[c['hero']])*(1+self.attempts[c['task_id']]))
                          for c in self.certificates],dtype=float)
        unknown=np.array([c['status']=='legal_reset_unknown' for c in self.certificates])
        result=np.zeros(len(weights))
        groups=[g for g in (~unknown,unknown) if g.any()]
        for group in groups: result[group]=weights[group]/weights[group].sum()/len(groups)
        return result

    def select(self,random):
        row=self.certificates[int(random.choice(len(self.certificates),p=self.probabilities()))]
        self.attempts[row['task_id']]+=1
        return row

    def state(self):
        result=super().state()
        result.update(sampling_kind='global_certified_unknown_mix_v1',
                      unknown_probability=.5 if any(c['status']=='legal_reset_unknown' for c in self.certificates) else 0.)
        return result


def validate_pool(pool,dll):
    if pool['goal']!=SEQUENCE_GOAL or pool.get('partition')!='train': raise ValueError('Training partition/goal required')
    certified=pool['tasks']; unknown=pool.get('unknown_tasks',[])
    if not certified or any(not eligible(c,dll,SEQUENCE_GOAL) for c in certified): raise ValueError('Stale certified task')
    current_data=data_signature()
    for row in unknown:
        binding=row['task']
        if (row['status']!='legal_reset_unknown' or row['admitter_sha256']!=file_hash(__file__)
            or row['data_signature']!=current_data or binding['rules_sha256']!=file_hash(dll)
            or binding['goal']!=SEQUENCE_GOAL or row['task_id']!=digest(binding)
            or split(binding['definition'])!='train' or row['admission']['legal_actions']<=0):
            raise ValueError('Invalid/stale unknown admission; never substitute a solvability proof')
    rows=certified+unknown
    if len({r['task_id'] for r in rows})!=len(rows): raise ValueError('Duplicate task binding')
    if any(split(r['task']['definition'])!='train' for r in rows): raise ValueError('Held-out task in training')
    return rows


def build(args):
    from train_ppo import Rules
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
    pool=json.loads(Path(args.old_pool).read_text());validate_pool(pool,args.dll)
    seen={digest(r['task']['definition']) for r in pool['tasks']+pool.get('unknown_tasks',[])}
    data=data_signature();rules=Rules();unknown=list(pool.get('unknown_tasks',[]));rejected=[]
    try:
        for row in json.loads(Path(args.drafts).read_text())['candidates']:
            definition=row['task_definition']
            if row['split']!='train': continue
            if split(definition)!='train': raise ValueError('False training split label')
            if digest(definition) in seen: continue
            try: current=rules.reset_task(definition)
            except RuntimeError as error:
                rejected.append(dict(draft_id=row['task_id'],reason=str(error),status='reset_failed_not_unsolvable'))
                continue
            if current['done'] or not current['legal']:
                rejected.append(dict(draft_id=row['task_id'],status='terminal_root_requires_independent_proof'))
                continue
            binding=dict(generator='independent_original_factors_v1',seed=definition['RandomSeed'],definition=definition,
                rules_sha256=file_hash(args.dll),feature_schema=rules.info['featureSchema'],catalog_sha256=digest(rules.actions),
                initial_state_sha256=digest(current['state']),goal=SEQUENCE_GOAL)
            unknown.append(dict(task_id=digest(binding),task=binding,hero=current['state']['hero'],status='legal_reset_unknown',
                data_signature=data,admitter_sha256=file_hash(__file__),admission=dict(legal_actions=len(current['legal']),
                native_done=False),scope='Real legal reset only; initial solvability unknown, never victory certification'))
            seen.add(digest(definition))
        if data_signature()!=data: raise ValueError('Runtime data changed during admission')
    finally: rules.close()
    result=copy.deepcopy(pool);result.update(unknown_tasks=unknown,admission_failures=rejected,
        sampling='Global mixed on-policy curriculum; unknown labels separate, no witness requirement for exploration')
    validate_pool(result,args.dll)
    path=Path(args.out)
    if path.exists(): raise ValueError('Preserve pool evidence')
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(certified=len(result['tasks']),unknown=len(unknown),admission_failures=len(rejected))))


def migrate(args):
    old=json.loads(Path(args.old_pool).read_text());new=json.loads(Path(args.new_pool).read_text())
    before=validate_pool(old,args.dll);after=validate_pool(new,args.dll)
    if any(r not in after for r in before): raise ValueError('Existing task was removed or edited')
    with np.load(args.checkpoint,allow_pickle=False) as arrays: header=json.loads(str(arrays['header']))
    metadata=header['metadata']
    if metadata['pool_sha256']!=file_hash(args.old_pool) or metadata['rules_sha256']!=file_hash(args.dll) or metadata['rules_data_sha256']!=data_signature()['sha256']:
        raise ValueError('Source checkpoint/pool/rules mismatch')
    parameters,optimizer,random,replay=load_checkpoint(args.checkpoint,metadata)
    if not replay['at_native_episode_boundary']: raise ValueError('Actual episode boundary required')
    expanded=copy.deepcopy(replay);expanded['sampler']['task_ids']=sorted(r['task_id'] for r in after)
    expanded['sampler']=MixedTaskSampler(after,expanded['sampler']).state()
    updated=dict(metadata,pool_sha256=file_hash(args.new_pool),mixed_task_sampler_sha256=file_hash(__file__))
    path=Path(args.out)
    if path.exists(): raise ValueError('Preserve migration evidence')
    path.mkdir(parents=True);save_checkpoint(path/'checkpoint.npz',optimizer,random,updated,expanded)
    restored,other,other_random,other_replay=load_checkpoint(path/'checkpoint.npz',updated)
    for key in parameters:
        for a,b in ((parameters[key],restored[key]),(optimizer.first[key],other.first[key]),(optimizer.second[key],other.second[key])):
            np.testing.assert_array_equal(a,b)
    assert random.bit_generator.state==other_random.bit_generator.state and optimizer.steps==other.steps
    for key in ('attempts','events','first_evidence'): assert replay['sampler'][key]==other_replay['sampler'][key]
    report=dict(status='verified_global_pool_migration',tasks=len(after),certified=len(new['tasks']),unknown=len(new['unknown_tasks']),
        parameter_and_adam_arrays_unchanged=3*len(parameters),rng_and_actual_history_unchanged=True,optimizer_updates=0,
        source_checkpoint_sha256=file_hash(args.checkpoint),new_checkpoint_sha256=file_hash(path/'checkpoint.npz'))
    (path/'migration.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('mode',choices=('build','migrate'))
    for name in ('dll','old-pool','out'): parser.add_argument('--'+name,required=True)
    for name in ('drafts','new-pool','checkpoint'): parser.add_argument('--'+name)
    args=parser.parse_args();(build if args.mode=='build' else migrate)(args)


if __name__=='__main__': main()
