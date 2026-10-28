"""Review retained real trajectories; never infer effect use from card type alone."""
import argparse,hashlib,json
from pathlib import Path
from trajectory_audit import classify_attack_sources

parser=argparse.ArgumentParser();parser.add_argument('run');args=parser.parse_args();folder=Path(args.run)
files=sorted(folder.glob('audit_*.json'))+sorted(folder.glob('first_actual*.json'))
sources=[];effects=set();sideways=set();unknown=set()
for file in files:
    saved=json.loads(file.read_text(encoding='utf-8'))
    if 'trace' not in saved:continue
    records=classify_attack_sources(saved['trace'])
    for record in records:
        key=record['card']+':'+record['mode']+':'+record['targets']
        if record['mode'].startswith(('play:','artifact:')):effects.add(key)
        elif record['mode']=='sideways':sideways.add(key)
        elif record['card']:unknown.add(key)
    sources.append({'file':str(file),'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),'seed':saved['seed'],'records':records})
result={'scope':'retained_trajectory_subset_only_not_complete_run_coverage','effect_attack_consumers':sorted(effects),'sideways_attack_consumers':sorted(sideways),'unknown_card_attack_modes':sorted(unknown),'sources':sources}
out=folder/'retained_attack_consumer_modes.json';out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'traces':len(sources),'effect_consumers':len(effects),'sideways_consumers':len(sideways),'unknown_modes':len(unknown),'actual_spell_effect_consumers':[x for x in sorted(effects) if x.startswith('magic_')],'evidence':str(out)},ensure_ascii=False))
