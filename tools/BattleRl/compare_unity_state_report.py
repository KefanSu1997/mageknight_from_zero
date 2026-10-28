"""Compare actual Unity click captures with their actual formal rule snapshots."""
import argparse
import json
from pathlib import Path
from task_feasibility import file_hash
from ui_state_mapping import battle_projection,compare_unity_capture


def compare_report(report):
    checked,errors=0,[]
    for index,step in enumerate(report['steps']):
        if not step['success'] or not step.get('hitObject'):
            errors.append(dict(step=index,error='Actual pointer interaction failed or lacks raycast evidence'))
        for side in ('beforeState','afterState'):
            capture={row['key']:row['value'] for row in step[side]}
            raw=capture.get('rl.sharedRuleState')
            if not raw:
                errors.append(dict(step=index,side=side,error='Missing actual rule snapshot'))
                continue
            state=json.loads(raw)
            differences=compare_unity_capture(state,capture)
            checked+=len(battle_projection(state))
            errors.extend(dict(step=index,side=side,**difference) for difference in differences)
    return dict(status='passed' if not errors and report['status']=='success' else 'failed',
                comparisons=checked,errors=errors,
                scope='Actual scene shared field mapping only; no saved PPO policy/full original acceptance claim')


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--report',required=True)
    parser.add_argument('--out',required=True)
    args=parser.parse_args()
    result=compare_report(json.loads(Path(args.report).read_text()))
    result['source_report']=dict(path=str(Path(args.report).resolve()),sha256=file_hash(args.report))
    path=Path(args.out)
    if path.exists():
        raise ValueError('Preserve actual comparison evidence')
    path.write_text(json.dumps(result,indent=2))
    print(json.dumps(result))
    if result['status']!='passed':
        raise SystemExit(1)


if __name__=='__main__':
    main()
