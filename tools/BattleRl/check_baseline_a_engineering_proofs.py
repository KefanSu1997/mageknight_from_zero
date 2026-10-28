"""Lossless storage and undefined-EV serialization evidence; no optimizer."""
import hashlib,json
from pathlib import Path
from baseline_a_scenario_ppo import finite_metrics
root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
assert finite_metrics({'train/explained_variance':float('nan'),'train/loss':.1},0)=={'train/explained_variance':None,'train/loss':.1}
assert finite_metrics({'train/explained_variance':.7},1)=={'train/explained_variance':.7}
for values,var in [({'train/loss':float('nan')},0),({'train/explained_variance':float('nan')},1)]:
    try:finite_metrics(values,var)
    except ValueError:pass
    else:raise AssertionError('Nonfinite optimization metric concealed')
proof=dict(compression=json.loads((root/'context_compression_full732.json').read_text()),
    runtime_gate=json.loads((root/'scenario_runtime_gate.json').read_text()),
    zero_variance_metric_gate=dict(passed=True,undefined_only_to_null=True,nonfinite_loss_rejected=True,optimizer_steps=0),
    source_sha256={n:hashlib.sha256((Path(__file__).parent/n).read_bytes()).hexdigest() for n in
        ('baseline_a_scenario_runtime.py','baseline_a_scenario_ppo.py')})
(root/'engineering_equivalence.json').write_text(json.dumps(proof,indent=2,allow_nan=False));print(json.dumps(proof['zero_variance_metric_gate']))
