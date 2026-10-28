"""Actual serial/parallel task equality and per-worker failure isolation gate."""
import copy
import json
from pathlib import Path
import tempfile
from solo_native_env import SoloNativeEnv
from solo_native_pool import SoloNativePool

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'


def run():
    evidence = Path(tempfile.mkdtemp(prefix='solo_parallel_gate_'))
    base = dict(Version=1, Hero='Tovak', DummyHero='Goldyx', DummyCrystals=[0,1,2,0],
                DualColorRule=1, Seeds=dict(Tiles=1, Player=191, Enemies=192, Mana=193,
                Offers=194, RoundShuffle=195, Progression=196, Dummy=197, Tactics=198,
                DummySkills=199, Ruins=200))
    setups = [copy.deepcopy(base) for _ in range(2)]
    setups[1]['Seeds']['Player'] = 2191
    reference = []
    def public(s):
        return {k: s[k] for k in ('observation','support_sha256','commands')}
    with SoloNativeEnv(DLL, ROOT, evidence/'serial', preparation=True) as env:
        for setup in setups:
            states = [public(env.reset(setup))]
            for _ in range(3):
                states.append(public(env.step(0)['state']))
            reference.append(states)
    timings = []
    with SoloNativePool(DLL, ROOT, evidence/'parallel', workers=2, preparation=True) as pool:
        def collect():
            rows = []
            while pool.pending:
                rows.extend(pool.poll(timeout=1))
            timings.extend(dict(worker=r.worker, task=r.task_id, operation=r.operation,
                                seconds=r.wall_seconds, error=r.error) for r in rows)
            return {r.worker:r for r in rows}
        for i, setup in enumerate(setups):
            pool.submit(i, f'fixture-{i}', 'reset', setup)
        try:
            pool.submit(0, 'fixture-0', 'reset', setups[0])
        except RuntimeError:
            pass
        else:
            raise AssertionError('Concurrent requests to the same process were accepted')
        rows = collect()
        for i in range(2):
            assert rows[i].error is None and public(rows[i].result) == reference[i][0]
        for step in range(1, 4):
            for i in range(2):
                pool.submit(i, f'fixture-{i}', 'step', 0)
            rows = collect()
            for i in range(2):
                assert rows[i].error is None and public(rows[i].result['state']) == reference[i][step]
        before = public(pool.envs[0].state)
        pool.submit(0, 'fixture-0', 'step', -1)
        pool.submit(1, 'fixture-1', 'export')
        rows = collect()
        assert rows[0].error and rows[1].error is None
        assert public(pool.envs[0].state) == before
        assert len(rows[1].result['records']) == 3
        invalid = copy.deepcopy(setups[0]); invalid['DummyHero'] = invalid['Hero']
        pool.submit(0, 'invalid-task', 'reset', invalid)
        assert collect()[0].error
        try:
            pool.submit(0, 'invalid-task', 'step', 0)
        except ValueError:
            pass
        else:
            raise AssertionError('Failed reset silently reused previous task')
    report = dict(passed=True, evidence=str(evidence), workers=2, decisions_per_task=3,
                  serial_parallel_equal=True, failed_reset_not_relabelled=True,
                  timings=timings, training_started=False)
    (evidence/'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    run()
