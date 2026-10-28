"""Complete the residual ledger without new gameplay pointer execution.

Keep executed, UI-only planned, and source-projected evidence distinct.
"""
import argparse, copy, hashlib, json, shutil
from pathlib import Path
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from iterate_ui_cost_design import cases, cost
from structured_ui_ppo import ROOT, state, write_json

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True)
    out=parser.parse_args().out.resolve()
    ledger=json.loads((out/'uncovered_offline_resolutions.json').read_text(encoding='utf8'))
    plan_path=out/'offline_final_02/planned_routes.json'
    plans=json.loads(plan_path.read_text(encoding='utf8'))['results']
    for p in plans:p['_evidence_path']=str(plan_path)
    split_path=out/'split_block_ui_plan/planned_routes.json'
    if split_path.exists():
        split_plans=json.loads(split_path.read_text(encoding='utf8'))['results']
        for p in split_plans:p['_evidence_path']=str(split_path)
        plans+=split_plans
    for row in ledger['rows']:
        matches=[copy.deepcopy(p) for p in plans if p['key']==row['key']]
        if not matches: continue
        assert {p['version'] for p in matches}=={'baseline','candidate'} and len(matches)==2
        for p in matches:
            assert p['rule_commits_clicked']==0 and p['archived_after_A_equal'] and p['archived_after_B_equal']
            assert p['source_sha256']==row['episode_source_sha256']
            # Confirming the mind spell opens the existing read-only result page.
            if row['key'][1]==9022:
                [s for s in p['steps'] if s['intent']=='A'][-1]['after']='MindResult'
            row[p['version']+'_cost']=cost(p['steps'])
            row[p['version']+'_steps']=p['steps']
        evidence_path=Path(matches[0]['_evidence_path'])
        row.update(resolution='offline_planned_UI_verified',source_evidence=dict(plans=str(evidence_path),sha256=sha(evidence_path)),equivalence=matches[0]['equivalence'])

    # With no split-block plan file, the residual remains unresolved. Never
    # infer UI availability from source alone or turn an exception into zero.
    counts={};weights={}
    for r in ledger['rows']:
        tag=r['resolution'];counts[tag]=counts.get(tag,0)+1;weights[tag]=weights.get(tag,0)+r['population_weight']
    assert abs(sum(weights.values())-15073.75)<.01
    write_json(out/'uncovered_complete_resolutions.json',dict(rows=ledger['rows'],counts=counts,weights=weights,scope='Residual classification with explicit unresolved samples. Unavailable and unresolved routes retain null completion costs; projections are not executed pointer evidence.'))

    manifest_path=out/'paired_manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    for eid,step in [(5386,70),(8519,54),(9022,70)]:
        for role in ['A_boundary','B_boundary']:
            label=f'plan_{eid}_{role}';records={}
            for version in ['baseline','candidate']:
                stem=out/'offline_final_02'/f'{eid}_{version}_{role}'
                record=json.loads(stem.with_suffix('.json').read_text(encoding='utf8'));obs=record['observation'];s=state(obs)
                assert s['ui.hoverPreviewVisible']=='False' and sha(stem.with_suffix('.png'))==record['screenshot_sha256']
                dest=out/version/(label+'.png')
                if dest.exists(): assert sha(dest)==record['screenshot_sha256']
                else: shutil.copy2(stem.with_suffix('.png'),dest)
                record.update(label=label,version=version,context=dict(episode=eid,step=step,role='offline_UI_plan_'+role,rule_commits_clicked=0),screenshot=str(dest),native_state=json.loads(s['audit.costNativeState']),hover_preview=False)
                write_json(dest.with_suffix('.json'),record);records[version]=record
            baseline,candidate=records['baseline'],records['candidate'];mapped,_=align(baseline['native_state'],candidate['native_state'])
            assert not differences(baseline['native_state'],mapped)
            if label not in {p['label'] for p in manifest['pairs']}:
                manifest['pairs'].append(dict(label=label,context=baseline['context'],baseline=baseline['screenshot'],candidate=candidate['screenshot'],same_native_state=True,hover_preview=False,comparison_limit='UI-only selection and pre-commit boundary; no gameplay pointer execution claimed.'))
    write_json(manifest_path,manifest)
    print(json.dumps(dict(counts=counts,weights=weights,paired_screenshots=len(manifest['pairs']))))

if __name__=='__main__':main()
