"""Capture-backed canvas/card-size audit and source delta of created panels."""
import argparse,hashlib,json,re
from pathlib import Path
from iterate_ui_cost_design import ROOT

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    manifest=json.loads((out/'paired_manifest.json').read_text(encoding='utf8'));shape_checks=[];errors=[];truncated=[];hover_checks=[]
    for pair in manifest['pairs']:
        records={v:json.loads(Path(pair[v]).with_suffix('.json').read_text(encoding='utf8')) for v in ('baseline','candidate')}
        for v,r in records.items():
            o=r['observation'];s={x['key']:x['value'] for x in o['state']}
            if (o['screenWidth'],o['screenHeight'])!=(1920,1080):errors.append(dict(label=pair['label'],version=v,canvas=[o['screenWidth'],o['screenHeight']]))
            expected=str(bool(pair.get('hover_preview',False)))
            hover_checks.append(dict(label=pair['label'],version=v,expected=expected,actual=s.get('ui.hoverPreviewVisible')))
            if s.get('ui.hoverPreviewVisible')!=expected:errors.append(dict(label=pair['label'],version=v,hover_mismatch=True))
            if s.get('ui.truncated'):truncated.append(dict(label=pair['label'],version=v,names=s['ui.truncated']))
        old={c['path']:c for c in records['baseline']['observation']['controls']}
        for c in records['candidate']['observation']['controls']:
            if not c['name'].startswith(('Btn_Card_','Btn_Unit_','Btn_Hand_','Btn_RestCard_','Btn_Target')) or c['path'] not in old:continue
            x=old[c['path']];check=dict(label=pair['label'],path=c['path'],baseline=[x['width'],x['height']],candidate=[c['width'],c['height']]);shape_checks.append(check)
            if max(abs(x['width']-c['width']),abs(x['height']-c['height']))>.1:errors.append(check)
    before=ROOT/'AutomationOutputs/UiFluency/design_versions/v0_baseline_reconstructable/source/Assets/Scripts/Battle'
    after=ROOT/'Assets/Scripts/Battle'
    pattern=re.compile(r'W\.Panel\(\s*[^,]+,\s*"([^"\n]+)"')
    def names(folder):
        return {n for file in folder.glob('PveBattleController*.cs') for n in pattern.findall(file.read_text(encoding='utf8'))}
    added=sorted(names(after)-names(before));removed=sorted(names(before)-names(after))
    source=(after/'PveBattleController.FluentUi.cs').read_text(encoding='utf8')
    # Explicitly allow only the known in-place tray in this source delta.
    added_classifications={name:dict(kind='local_tray',battle_bounds=[38,206,405,439],outside_bounds=[995,108,885,240]) if name=='InlineSource' else dict(kind='unclassified') for name in added}
    if any(x['kind']=='unclassified' for x in added_classifications.values()):errors.append(dict(unclassified_new_panels=added_classifications))
    result=dict(canvas=[1920,1080],pairs=len(manifest['pairs']),shape_checks=len(shape_checks),shape_details=shape_checks,hover_checks=hover_checks,truncated=truncated,errors=errors,added_panels=added_classifications,removed_panel_names=removed,no_added_full_canvas_page=not any(x['kind']=='unclassified' for x in added_classifications.values()),page_definition='A separate replacement/shield of the full main canvas; local trays are listed separately and their interactions remain counted.',source_sha256=hashlib.sha256(source.encode()).hexdigest(),scope='All supplied captures and all newly named controller panels. Does not assert reachability or complete review of unobserved legacy screens.')
    result['baseline_truncated']=[r for r in truncated if r['version']=='baseline']
    result['candidate_truncated']=[r for r in truncated if r['version']=='candidate']
    result['truncation_gate']='Candidate must have no truncated text. Retain baseline defects as comparison evidence; repairing them must not fail candidate acceptance.'
    (out/'design_constraints.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('shape_details','hover_checks')},ensure_ascii=False))
    if errors or result['candidate_truncated']:raise AssertionError('UI design constraints failed')
if __name__=='__main__':main()
