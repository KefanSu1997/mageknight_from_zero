"""Real two-process CPU transport gate; never authorizes formal training."""
import hashlib
import json
from pathlib import Path
import tempfile
from solo_native_env import SoloNativeEnv, NativeRuleError

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'


def run():
    evidence = Path(tempfile.mkdtemp(prefix='solo_native_transport_gate_'))
    try:
        SoloNativeEnv(DLL, ROOT, evidence/'formal_refusal')
    except NativeRuleError as exc:
        assert 'Formal training gate incomplete' in str(exc)
    else:
        raise AssertionError('Preparation adapter incorrectly allowed formal training')
    setup = dict(Version=1, Hero='Tovak', DummyHero='Goldyx', DummyCrystals=[0,1,2,0],
                 DualColorRule=1, Seeds=dict(Tiles=1, Player=191, Enemies=192, Mana=193,
                 Offers=194, RoundShuffle=195, Progression=196, Dummy=197, Tactics=198,
                 DummySkills=199, Ruins=200))
    with SoloNativeEnv(DLL, ROOT, evidence/'first', preparation=True) as first, \
            SoloNativeEnv(DLL, ROOT, evidence/'second', preparation=True) as second:
        first.reset(setup)
        first.step(0, log_probability=-1.0, value=0.0)
        receipt = first.save(evidence/'episode.json')
        encoded = (evidence/'episode.json').read_bytes()
        assert hashlib.sha256(encoded).hexdigest() == receipt['sha256']
        saved = json.loads(encoded)
        second.restore(saved['native'])
        for k in ('observation', 'support_sha256', 'commands'):
            assert first.state[k] == second.state[k], k
        for env in (first, second):
            old = env.state
            try:
                env.step(len(old['commands']))
            except NativeRuleError:
                pass
            else:
                raise AssertionError('Invalid local action accepted')
            assert env.state is old
        a, b = first.step(0), second.step(0)
        for k in ('observation', 'support_sha256', 'commands'):
            assert a['state'][k] == b['state'][k], k
        assert a['trajectory']['Transition'] == b['trajectory']['Transition']
        assert all(t['wall_seconds'] >= 0 for t in first.timings+second.timings)
        report = dict(passed=True, evidence=str(evidence), formal_training_refused=True,
                      independently_restored_next_transition_equal=True, receipt=receipt,
                      timings=first.timings, training_started=False)
        (evidence/'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report))


if __name__ == '__main__':
    run()
