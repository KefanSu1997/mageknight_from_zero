"""Audited complete-episode targets; gamma=lambda=1, no partial bootstrapping."""
import hashlib
import json
from pathlib import Path
import numpy as np
from solo_trajectory_audit import audit_native_journal


class SoloEpisodeData:
    def __init__(self, result_path):
        self.collection_directory=Path(result_path).resolve().parent.parent
        self.result=json.loads(Path(result_path).read_text(encoding='utf-8'))
        if self.result.get('status')!='complete' or not self.result.get('eligible_complete_episode'):
            raise ValueError('Incomplete/quarantined scenario cannot enter complete-episode PPO')
        data=Path(self.result['archive']['path']).read_bytes()
        if hashlib.sha256(data).hexdigest()!=self.result['archive']['sha256']:
            raise ValueError('Archived episode SHA differs')
        native=json.loads(data)['native']
        self.records=native['records']
        audit=audit_native_journal(self.records)
        if not native['terminal'] or not audit['passed']:
            raise ValueError('Complete native episode audit failed')
        self.actions=np.array([r['SelectedIndex'] for r in self.records],dtype=np.int64)
        self.old_log_probs=np.array([r['BehaviorLogProbability'] for r in self.records],dtype=np.float64)
        self.values=np.array([r['ValuePrediction'] for r in self.records],dtype=np.float64)
        self.rewards=np.array([r['Transition']['Reward'] for r in self.records],dtype=np.float64)
        if not all(np.isfinite(x).all() for x in (self.old_log_probs,self.values,self.rewards)):
            raise ValueError('Missing/nonfinite behavior statistics')
        # Monte Carlo returns equal GAE targets with gamma=lambda=1 at a true terminal.
        self.returns=np.cumsum(self.rewards[::-1])[::-1].copy()
        self.advantages=self.returns-self.values
        self.episode_starts=np.zeros(len(self.records),dtype=bool);self.episode_starts[0]=True

    def prepared_steps(self,builder):
        # Only public observation/support enter the network; private journal
        # state hashes and setup RNG never become policy features.
        for record in self.records:
            yield builder.prepare_record(record)


def state_signature(state):
    """Stable tensor/content binding independent of torch archive zip metadata."""
    import torch
    digest=hashlib.sha256()
    for name,value in sorted(state.items()):
        digest.update(name.encode());digest.update(b'\0')
        if torch.is_tensor(value):
            tensor=value.detach().cpu().contiguous()
            digest.update(str(tensor.dtype).encode())
            digest.update(json.dumps(list(tensor.shape)).encode())
            digest.update(tensor.numpy().tobytes())
        else:digest.update(json.dumps(value,sort_keys=True,separators=(',',':')).encode())
    return digest.hexdigest()


def verify_behavior_binding(episode,network,builder):
    import torch
    directory=episode.collection_directory
    binding=json.loads((directory/'behavior_binding.json').read_text())
    if binding.get('scripted_clock_fixture'):raise ValueError('Scripted fixture is not on-policy PPO data')
    data=directory/'behavior_policy.pt'
    if hashlib.sha256(data.read_bytes()).hexdigest()!=binding['sha256']:
        raise ValueError('Behavior checkpoint SHA differs')
    if (binding['schema_sha256']!=builder.schema.signature
        or binding['catalog_signature']!=builder.text_assets.catalog.signature):
        raise ValueError('Behavior input contract differs')
    saved=torch.load(data,map_location='cpu',weights_only=True)
    if state_signature(saved)!=state_signature(network.state_dict()):
        raise ValueError('Current policy differs from the policy that collected these episodes')
