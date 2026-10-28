"""Persistence/cache integrity gate; controlled reader, no model execution."""
import json,tempfile,hashlib
from pathlib import Path
import numpy as np
from baseline_a_scenario_runtime import ScenarioRuntime,packed
from baseline_a_scenario_actions import prepare_support
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets


def run():
    source=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001/episode_000000/native.json')
    rows=json.loads(source.read_text())['native']['records'][:3]
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    assets=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    supports=[prepare_support(json.loads(r['ObservationBefore']),r['CommandsJson'],schema,assets) for r in rows]
    class Reader:
        calls=0
        def encode_many(self,documents):
            self.calls+=1;self.last_lengths=[1]*len(documents)
            return [np.random.default_rng(int(hashlib.sha256(packed(d)).hexdigest()[:8],16)).normal(size=(len(d['options'])+1,384)).astype(np.float32) for d in documents]
    binding={'gate':'controlled_reader_only','source_sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
    with tempfile.TemporaryDirectory() as tmp:
        rt=ScenarioRuntime(tmp,binding);reader=Reader();rt.reader=reader
        ids=[]
        for i,s in enumerate(supports):
            doc=dict(state='控制检查状态 '+str(i),rules='',question='选择完整合法操作')
            c=rt.add(s,doc);assert rt.add(s,doc)!=c;ids.append(c)
        requests=[(c,rt.tree(c)[0].branch(0)) for c in ids]
        first=rt.vectors(requests);assert reader.calls==1
        rt.vectors(requests);assert reader.calls==1
        rt.close()
        loaded=ScenarioRuntime(tmp,binding)
        for c,s in zip(ids,supports):
            recovered=loaded.support(c)
            assert recovered['commands']==s['commands']
            np.testing.assert_array_equal(recovered['descriptors'],s['descriptors'])
        for a,b in zip(first,loaded.vectors(requests)):np.testing.assert_array_equal(a,b)
        assert loaded.reader is None
        # Tamper must fail before it can silently change a PPO action mapping.
        loaded.db.execute('UPDATE contexts SET payload=? WHERE id=?',(b'{}',ids[0]));loaded.db.commit()
        try:loaded._read(ids[0])
        except ValueError:pass
        else:raise AssertionError('Corrupted context accepted')
        loaded.close()
        try:ScenarioRuntime(tmp,{'gate':'wrong'})
        except ValueError:pass
        else:raise AssertionError('Mismatched binding accepted')
    return dict(passed=True,contexts=len(ids),actual_complete_candidates=sum(len(s['commands']) for s in supports),
                persisted_supports_exact=True,persisted_vectors_exact=True,reader_batches=reader.calls,
                corruption_rejected=True,binding_mismatch_rejected=True,optimizer_steps=0,
                scope='Persistence and controlled vector cache only; real semantic rendering/encoder/CUDA/full rollout pending')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/scenario_runtime_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
