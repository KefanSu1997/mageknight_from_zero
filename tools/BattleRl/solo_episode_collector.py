"""Concurrent complete-scenario collection with immutable native journals.

The caller owns the frozen behavior policy and native pool. Engineering limits
produce resumable partials, never terminal rewards or completed PPO episodes.
"""
from collections import deque
from pathlib import Path
import hashlib
import json
import os
import time
import uuid
import torch
from solo_trajectory_audit import audit_native_journal
from solo_progress import publish_progress


class SoloBehavior:
    def __init__(self, model, builder):
        self.model=model.eval(); self.builder=builder

    def begin(self, seed):
        return dict(memory=None,generator=torch.Generator(device='cpu').manual_seed(seed))

    def select(self, state, context):
        start=time.perf_counter(); prepared=self.builder.prepare(state)
        prepare=time.perf_counter()-start; start=time.perf_counter()
        with torch.inference_mode():
            logits,value,memory=self.model.step(prepared,context['memory'])
            logp=torch.log_softmax(logits,0)
            if not torch.isfinite(logp).all() or not torch.isfinite(value):
                raise ValueError('Nonfinite behavior distribution/value')
            index=int(torch.multinomial(logp.exp().cpu(),1,generator=context['generator']))
            context['memory']=memory
        return dict(index=index,log_probability=float(logp[index]),value=float(value),
                    prepare_seconds=prepare,inference_seconds=time.perf_counter()-start)

    def snapshot(self, context):
        memory=context['memory']
        return dict(memory=None if memory is None else tuple(tuple(x.cpu().clone() for x in pair) for pair in memory),
                    generator=context['generator'].get_state())

    def save_policy(self, path):
        torch.save(self.model.state_dict(),path)
        return dict(sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                    schema_sha256=self.builder.schema.signature,
                    catalog_signature=self.builder.text_assets.catalog.signature)


def collect_scenarios(pool, behavior, tasks, output, *, max_decisions=None, ready_batch=False):
    """Collect every explicitly assigned task, including failures, in input order.

    A task has id/setup/actor_seed. Different workers never share actor RNG or
    recurrent memory. No replacement world is selected after a task fails.
    Numeric subdirectories avoid accepting task IDs as filesystem paths.
    """
    output=Path(output); output.mkdir(parents=True,exist_ok=True)
    tasks=list(tasks)
    ids=[task['id'] for task in tasks]
    if len(set(ids))!=len(ids) or any(not isinstance(x,str) or not x for x in ids):
        raise ValueError('Unique nonempty task IDs required')
    if max_decisions is not None and (type(max_decisions) is not int or max_decisions<1):
        raise ValueError('Invalid engineering decision limit')
    if pool.pending:raise ValueError('Collector requires an idle pool')
    if ready_batch and not callable(getattr(behavior,'select_many',None)):
        raise ValueError('Ready batching requires an explicit batch behavior')
    queue=deque(enumerate(tasks)); active={}; results={}; started=time.perf_counter()
    binding=behavior.save_policy(output/'behavior_policy.pt')
    (output/'behavior_binding.json').write_text(json.dumps(binding,indent=2),encoding='utf-8')

    def assign(worker):
        if not queue:return
        number,task=queue.popleft(); directory=output/f'episode_{number:06d}'
        directory.mkdir(exist_ok=False)
        (directory/'task.json').write_text(json.dumps(task,indent=2),encoding='utf-8')
        active[worker]=dict(number=number,task=task,directory=directory,context=behavior.begin(task['actor_seed']),
                            decisions=0,return_scaled=0.,started=time.perf_counter(),timings=[],initialized=False)
        pool.submit(worker,task['id'],'reset',task['setup'])

    def finish(worker,status,error=None):
        run=active[worker]; directory=run['directory']; task_id=run['task']['id']
        report=dict(task_id=task_id,status=status,error=error,decisions=run['decisions'],
                    return_scaled=run['return_scaled'],timings=run['timings'])
        if run['initialized']:
            try:
                saved=pool.envs[worker].save(directory/'native.json')
                report['archive']=saved
                native=json.loads((directory/'native.json').read_text(encoding='utf-8'))['native']
                if status=='complete':
                    report['audit']=audit_native_journal(native['records'])
                    if (not native['terminal'] or not report['audit']['passed']
                        or abs(report['audit']['return_scaled']-run['return_scaled'])>1e-8):
                        report['status']='audit_failed'
                # Retain recurrent memory/RNG even on engineering interruption.
                torch.save(behavior.snapshot(run['context']),directory/'behavior_context.pt')
            except Exception as exc:
                report['status']='archive_failed';report['archive_error']=repr(exc)
        report['wall_seconds']=time.perf_counter()-run['started']
        report['eligible_complete_episode']=report['status']=='complete'
        (directory/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
        results[run['number']]=report;del active[worker];assign(worker)

    for worker in range(len(pool.envs)):assign(worker)
    while active:
        ready=[]
        for row in pool.poll(timeout=1):
            run=active[row.worker]
            run['timings'].append(dict(operation=row.operation,seconds=row.wall_seconds))
            if row.error is not None:
                finish(row.worker,'native_failed',row.error);continue
            if row.operation=='reset':run['initialized']=True;state=row.result
            else:
                state=row.result['state'];run['decisions']+=1
                run['return_scaled']+=row.result['trajectory']['Transition']['Reward']
                transition=getattr(behavior,'transition',None)
                if transition is not None:
                    try:transition(row.result,run['context'])
                    except Exception as exc:
                        finish(row.worker,'behavior_transition_failed',repr(exc));continue
            progress=dict(elapsed_seconds=time.perf_counter()-started,finished=len(results),
                          active=[dict(worker=w,task_id=r['task']['id'],decisions=r['decisions'],
                                       return_scaled=r['return_scaled']) for w,r in active.items()])
            publish_progress(output/'progress.json',progress)
            if state['observation']['Terminal']:
                finish(row.worker,'complete');continue
            if max_decisions is not None and run['decisions']>=max_decisions:
                finish(row.worker,'engineering_partial');continue
            if ready_batch:
                ready.append((row.worker,state));continue
            try:
                choice=behavior.select(state,run['context'])
                run['timings'].append(dict(operation='policy',**choice))
                pool.submit(row.worker,run['task']['id'],'step',choice['index'],
                            log_probability=choice['log_probability'],value=choice['value'])
            except Exception as exc:
                finish(row.worker,'policy_failed',repr(exc))
        if ready:
            # Only ready environments participate; no barrier waits for slow tasks.
            # A batch failure is preserved, never retried after consuming task RNG.
            try:
                choices=behavior.select_many([state for _,state in ready],
                                             [active[w]['context'] for w,_ in ready])
                if len(choices)!=len(ready):raise ValueError('Batch result count differs')
            except Exception as exc:
                for worker,_ in ready:finish(worker,'policy_failed',repr(exc))
            else:
                for (worker,_),choice in zip(ready,choices):
                    run=active[worker]
                    try:
                        run['timings'].append(dict(operation='policy',**choice))
                        pool.submit(worker,run['task']['id'],'step',choice['index'],
                                    log_probability=choice['log_probability'],value=choice['value'])
                    except Exception as exc:
                        finish(worker,'policy_failed',repr(exc))
    ordered=[results[i] for i in range(len(tasks))]
    report=dict(episodes=ordered,requested=len(tasks),
                complete=sum(x['eligible_complete_episode'] for x in ordered),
                wall_seconds=time.perf_counter()-started)
    (output/'collection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    publish_progress(output/'progress.json',dict(elapsed_seconds=report['wall_seconds'],
                         finished=len(results),complete=report['complete'],active=[]))
    return report
