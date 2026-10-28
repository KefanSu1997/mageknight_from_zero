"""Actual saved-head gate on archived complete native supports; no optimization."""
import copy
import io
import json
from pathlib import Path
import zipfile
import numpy as np
import torch
from baseline_a_scenario_actions import prepare_support
from baseline_a_scenario_candidate_head import ScenarioCandidateHead
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from hierarchical_option_policy import ChoiceTree
from structured_hierarchy_core import StructuredHierarchy


def run(root):
    torch.set_num_threads(2);torch.manual_seed(7112026)
    path=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001/episode_000000/native.json')
    records=json.loads(path.read_text())['native']['records'][:16]
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    text=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    supports=[prepare_support(json.loads(r['ObservationBefore']),r['CommandsJson'],schema,text) for r in records]
    with zipfile.ZipFile(root/'reference_checkpoint.zip') as z:
        saved=torch.load(io.BytesIO(z.read('policy.pth')),weights_only=True,map_location='cpu')
    head=ScenarioCandidateHead();head.load_reference(saved);head.eval()
    latent=torch.randn(len(supports),128)*.1
    scores=head.flat_logits(latent,supports)
    batch_error=0.;tree_error=0.;loss=0.
    for row,(support,score) in enumerate(zip(supports,scores)):
        isolated=head.flat_logits(latent[row:row+1],[support])[0]
        batch_error=max(batch_error,float((isolated-score).abs().max().detach()))
        tree=ChoiceTree(support['operations']);core=StructuredHierarchy(tree,score)
        logp=torch.stack([core.log_prob(i) for i in range(len(score))])
        tree_error=max(tree_error,float((logp-score.log_softmax(0)).abs().max().detach()))
        assert torch.all(logp.exp()>0) and torch.allclose(logp.exp().sum(),torch.tensor(1.),atol=1e-5)
        assert support['commands']==tuple(records[row]['CommandsJson'])
        loss=loss-logp[int(records[row]['SelectedIndex'])]
    assert batch_error<1e-5 and tree_error<1e-5
    loss.backward()
    gradient=sum(float(p.grad.abs().sum()) for p in head.argument_encoder.parameters() if p.grad is not None)
    assert gradient>0 and all(p.grad is None or torch.isfinite(p.grad).all() for p in head.parameters())
    # Transport row number must not influence action scoring.
    row=records[0];changed=[]
    for c in row['CommandsJson']:
        item=json.loads(c);item['ExpectedStep']+=100;changed.append(json.dumps(item))
    renumbered=json.loads(row['ObservationBefore']);renumbered['Step']+=100
    updated=prepare_support(renumbered,changed,schema,text)
    np.testing.assert_array_equal(updated['descriptors'],supports[0]['descriptors'])
    reversed_support=prepare_support(json.loads(row['ObservationBefore']),row['CommandsJson'][::-1],schema,text)
    reverse_error=float((head.flat_logits(latent[:1],[reversed_support])[0]-scores[0].flip(0)).abs().max().detach())
    assert reverse_error<1e-5
    reloaded=ScenarioCandidateHead();reloaded.load_state_dict(head.state_dict());reloaded.eval()
    with torch.no_grad():
        for left,right in zip(scores,reloaded.flat_logits(latent,supports)):torch.testing.assert_close(left,right,rtol=0,atol=0)
    return dict(passed=True,actual_supports=len(supports),complete_candidates=sum(len(s['commands']) for s in supports),
        batched_vs_single_max_abs=batch_error,zero_residual_hierarchy_max_abs=tree_error,
        permutation_max_abs=reverse_error,candidate_gradient_l1=gradient,
        nonzero_printed_components=sum(int(np.any(s['descriptors'][:,96:],axis=1).sum()) for s in supports),
        optimizer_steps=0,scope='Actual A candidate head and complete supports; contextual reader, full recurrent policy and CUDA PPO pending')


if __name__=='__main__':
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec');result=run(root)
    (root/'scenario_candidate_head_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
