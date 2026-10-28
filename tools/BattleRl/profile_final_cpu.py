import os,sys,json,time,cProfile,pstats,io
from pathlib import Path
os.environ['OPENBLAS_NUM_THREADS']='1'
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
from final_token_option import semantic_document
from final_token_runtime import node_document
from hierarchical_option_policy import ChoiceTree
root=Path('remote_final_semantic300')
timings=[json.loads(x) for x in (root/'contexts/encoder_timing.jsonl').read_text().splitlines()]
summary=dict(calls=len(timings),nodes=sum(x['nodes'] for x in timings),encoder_seconds=sum(x['seconds'] for x in timings),tokens=sum(x['tokens'] for x in timings))
docs=[]
with (root/'contexts/contexts.jsonl').open() as f:
    for i,line in enumerate(f):
        if i>=256:break
        docs.append(json.loads(line))
old=[]
with Path('remote_green_enhanced300/contexts/contexts.jsonl').open() as f:
    for i,line in enumerate(f):
        if i>=128:break
        old.append(json.loads(line))
profile=cProfile.Profile();profile.enable()
for doc in docs:
    tree=ChoiceTree(doc['operations'])
    for n in [tree.branch(0)]+[i for i,v in enumerate(tree.nodes) if len(v['children'])>1][:5]:node_document(tree,doc,n)
for doc in old:semantic_document(doc)
profile.disable();s=io.StringIO();pstats.Stats(profile,stream=s).sort_stats('cumulative').print_stats(24)
summary['cpu_profile']=s.getvalue();print(json.dumps(summary,indent=2))
Path('optimization_stage_20261014/final_cpu_profile.json').write_text(json.dumps(summary,indent=2))
