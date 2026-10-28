"""Read-only extraction of a recorded decision, not a new model rollout."""
import os,sys,json,hashlib
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
from rollout_archive import load
import hierarchical_option_policy as h
import green_option_descriptions as descriptions
descriptions.install()
root=Path('remote_green_enhanced300');context=36
with (root/'contexts/contexts.jsonl').open() as f:
    for i,line in enumerate(f,1):
        if i==context:doc=json.loads(line);context_sha=hashlib.sha256(line.encode()).hexdigest();break
with (root/'contexts/selection_paths.jsonl').open() as f:
    route=next(json.loads(line) for line in f if json.loads(line)['context']==context)
buffer=load(root/'rollout_0000_step0.archive')
import numpy as np
index=int(np.flatnonzero(buffer.observations['context_id'].reshape(-1)==context)[0])
action=int(buffer.actions.reshape(-1)[index]);assert action==route['leaf']
tree=h.ChoiceTree(doc['operations']);node=route['route'][1]['node']
report=dict(run=root.name,behavior_update=0,context=context,rollout_row=index,action=action,
    log_probability=float(buffer.log_probs.reshape(-1)[index]),reward=float(buffer.rewards.reshape(-1)[index]),
    route=route,document=tree.document(doc,node),complete_operation=doc['operations'][action],
    original_document=doc,source_context_sha=context_sha)
out=Path('optimization_stage_20261014/green_training_example36.json');out.write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps({k:v for k,v in report.items() if k not in ('original_document','document')}))
