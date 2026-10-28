"""Read-only evidence review of the saved complete A-aligned gate games."""
import os,json,collections
from pathlib import Path
os.chdir(Path(__file__).resolve().parents[2])
from solo_episode_data import SoloEpisodeData
from solo_rollout_review import review_episodes
root=Path('/root/solo_A_aligned_cuda_gate_20261022')
episodes=[SoloEpisodeData(root/f'collection/episode_{i:06d}/result.json') for i in range(7)]
review=review_episodes(episodes)
for e,row in zip(episodes,review['tasks']):
    kinds=collections.Counter();examples=[];available=collections.Counter();fractions=collections.defaultdict(list)
    for r in e.records:
        cmd=json.loads(r['CommandsJson'][r['SelectedIndex']])
        kinds.update(k for k,v in cmd.items() if v is not None and k not in ('SchemaVersion','ExpectedStep'))
        counts=collections.Counter()
        for raw in r['CommandsJson']:
            candidate=json.loads(raw)
            counts.update(k for k,v in candidate.items() if v is not None and k not in ('SchemaVersion','ExpectedStep'))
        if len(r['CommandsJson'])>1:
            available.update(counts.keys())
            for key,count in counts.items():fractions[key].append(count/len(r['CommandsJson']))
        if len(examples)<8:examples.append(dict(command=cmd,reward=r['Transition']['Reward'],transition=r['Transition']))
    row['operation_counts']=dict(kinds);row['first_eight']=examples
    row['available_nontrivial_states']=dict(available)
    row['mean_candidate_fraction_when_available']={k:sum(v)/len(v) for k,v in fractions.items()}
    row['last_transition']=e.records[-1]['Transition']
out=root/'exposure_review.json';out.write_text(json.dumps(review,ensure_ascii=False,indent=2))
print(json.dumps(dict(status=review['status'],tasks=[{k:v for k,v in r.items() if k not in ('first_eight','last_transition')} for r in review['tasks']]),ensure_ascii=False))
