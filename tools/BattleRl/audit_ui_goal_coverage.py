"""Do not promote covered-route metrics into an unproven full-population claim."""
import argparse,hashlib,json
from pathlib import Path
from structured_ui_ppo import ROOT,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    population_path=ROOT/'AutomationOutputs/UiFluency/computed_costs/costs.json';population=json.loads(population_path.read_text(encoding='utf8'))
    cost=json.loads((out/'cost_comparison.json').read_text(encoding='utf8'));old,new=cost['versions']['baseline'],cost['versions']['candidate']
    known=population['aggregate']['measured_mass'];missing=population['population_transitions']-known
    assert abs(old['weight']-known)<.01 and abs(new['weight']-known)<.01
    b0=known*old['b_clicks'];b1=known*new['b_clicks'];p0=known*old['transition_px'];p1=known*new['transition_px']
    changed=[]
    for name,sha in cost['design_source_sha256'].items():
        if hashlib.sha256((ROOT/'Assets/Scripts/Battle'/name).read_bytes()).hexdigest()!=sha:changed.append(name)
    result=dict(goal_complete=False,covered_weight=known,population=population['population_transitions'],missing_weight=missing,incompatible_weight=population['aggregate']['incompatible_mass'],unknown_weight=population['aggregate']['unknown_mass'],covered_click_reduction=1-b1/b0,covered_path_reduction=1-p1/p0,source_binding_mismatches=changed,full_population_click_goal_proven=False,full_population_path_goal_proven=False,missing_not_filled_with_zero=True,
        unchanged_missing_click_sensitivity=[dict(assumed_old_and_new_missing_clicks=c,full_population_reduction=(b0-b1)/(b0+missing*c),assumption_only=True) for c in (1,3,5,10)],
        max_unchanged_missing_click_mean_to_meet_30pct=(.7*b0-b1)/(.3*missing),
        interpretation='Sensitivity rows are assumptions, not measurements. Covered-route targets do not prove full-population targets. Missing costs remain null. No retroactive expansion of denominator.',population_source_sha256=hashlib.sha256(population_path.read_bytes()).hexdigest())
    write_json(out/'goal_coverage_audit.json',result);print(json.dumps(result,ensure_ascii=False,indent=2))
    if changed:raise AssertionError(changed)
if __name__=='__main__':main()
