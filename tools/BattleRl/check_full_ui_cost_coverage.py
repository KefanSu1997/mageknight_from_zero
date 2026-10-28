"""Fail-closed completeness check before overall UI cost optimization export."""
import argparse
import json
from pathlib import Path
import sqlite3


def check(folder):
    folder=Path(folder)
    manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
    db=sqlite3.connect('file:'+str((folder/'workset.sqlite').resolve()).replace('\\','/')+'?mode=ro',uri=True)
    try:
        tables={r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        measured=0;missing=manifest['totals']['steps'];invalid=0
        if 'validated_cost_steps' in tables:
            missing=db.execute('''SELECT COUNT(*) FROM steps s LEFT JOIN validated_cost_steps c
              ON s.episode=c.episode AND s.ordinal=c.ordinal AND s.action=c.action
              WHERE c.episode IS NULL OR c.validated!=1''').fetchone()[0]
            invalid=db.execute('''SELECT COUNT(*) FROM validated_cost_steps
              WHERE validated!=1 OR clicks<0 OR panel_changes<0 OR within_distance_px<0
              OR approach_distance_px<0 OR actual_source_sha IS NULL''').fetchone()[0]
            measured=manifest['totals']['steps']-missing
        result=dict(status='ready' if missing==0 and invalid==0 else 'incomplete',
            cost_export_ready=missing==0 and invalid==0,required_rule_steps=manifest['totals']['steps'],
            validated_rule_steps=measured,missing_rule_steps=missing,invalid_cost_rows=invalid,
            required_within_episode_transitions=manifest['totals']['transitions'],
            required_action_kinds=manifest['kind_count'],required_argument_variants=manifest['variants']['actions'],
            required_parameter_and_phase_pair_variants=manifest['variants']['pair_variants'],
            scope='must validate every requested replay step; no winner-only or supported-subset denominator',
            transition_contract='approach distance is counted once on the following step; internal distance stays within that step; use sequential native commits')
        return result
    finally:db.close()


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('folder',type=Path);args=p.parse_args()
    result=check(args.folder)
    path=args.folder/'coverage_check.json';path.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
    if not result['cost_export_ready']:raise SystemExit(2)
