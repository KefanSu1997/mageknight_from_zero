"""Export a real state for human review; no generated game facts."""
import os,sys,json
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
from final_token_option import semantic_document
root=Path('optimization_stage_20261014')
example=json.loads((root/'green_training_example36.json').read_text())
doc=semantic_document(example['original_document'])
(root/'semantic_state_review36.txt').write_text(doc['state'],encoding='utf-8')
(root/'semantic_state_review36.json').write_text(json.dumps(dict(source_run=example['run'],context=36,source_context_sha=example['source_context_sha'],state=doc['state'],scope='Public-state summary proposed for new fresh run; original structured observation retained separately. Summary is not exhaustive.'),ensure_ascii=False,indent=2))
print('Exported actual context36 public state for review')
