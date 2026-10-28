"""Read-only, streaming historical rules-trace inventory and UI cost audit.

Rule actions are not clicks. Exact duplicate exported files are counted once;
per-run strata are retained. Unknown UI mapping has null cost, never zero.
"""
import argparse
import collections
import csv
import hashlib
import html
import json
import math
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BACKUP = Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def candidate(path):
    p = Path(path)
    if p.suffix != '.json' or not (p.name.startswith('episode_') or
            p.name.startswith('worker_') and '_episode_' in p.name):
        return False
    if any(any(w in s.lower() for w in ('eval', 'diagnos', 'calibration', 'discovery')) for s in p.parts):
        return False
    return True


def group(kind):
    if kind in ('source', 'extraSource', 'crystal', 'payment', 'paymentClear', 'cancelPayment'):
        return '魔力取得与付款'
    if kind in ('play', 'sideways', 'artifact', 'boxReveal', 'skillShapeshift'):
        return '使用卡牌'
    if kind.startswith('damage') or kind in ('assignDamage','unitDamage'):
        return '承伤规划与提交'
    if kind in ('attack', 'block', 'blockSelected', 'advance', 'fight', 'defend', 'explosiveArmor', 'clearDefend'):
        return '战斗目标、结算与阶段'
    if kind.startswith('unit'):
        return '部队操作'
    if kind.startswith('skill') or kind.startswith('shared'):
        return '技能操作'
    if kind in ('nextTurn', 'rest', 'roundFinish', 'roundReturn', 'tactic'):
        return '回合与休息'
    return '其他：待细分'


def physical_play(row):
    b, a, action = row['before'], row['after'], row['action']
    old = {x['instance'] for z in ('played', 'removed', 'roundAside', 'banners')
           for x in b.get('zones', {}).get(z, [])}
    hand = {x['instance']: x for x in b.get('zones', {}).get('hand', [])}
    return any(x['instance'] not in old and x['instance'] in hand and
               hand[x['instance']]['id'] == action.get('Card')
               for z in ('played', 'removed', 'roundAside', 'banners')
               for x in a.get('zones', {}).get(z, []))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--backup', type=Path, default=DEFAULT_BACKUP)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    manifest_bytes = (args.backup/'files.sha256.jsonl').read_bytes()
    entries = [json.loads(l) for l in manifest_bytes.splitlines()]
    archive = [r for r in entries if r['path'].startswith('AutomationOutputs/BattleRl/') and candidate(r['path'])]
    # Physical duplicate exports, including root/rollouts hardlinks. Repeated
    # identical episode bytes also collapse; this conservative scope is explicit.
    seen, unique, duplicates = set(), [], []
    for r in archive:
        if r['sha256'] in seen:
            duplicates.append(r)
        else:
            seen.add(r['sha256']); unique.append(r)
    locals_ = []
    for p in sorted((ROOT/'AutomationOutputs/BattleRl').rglob('*.json')):
        if candidate(p):
            raw = p.read_bytes(); sha = digest(raw)
            if sha not in seen:
                seen.add(sha); locals_.append(dict(path=p.relative_to(ROOT).as_posix(),
                    sha256=sha, bytes=len(raw), local=True))
    sources = unique+locals_
    mode_path = ROOT/'AutomationOutputs/BattleRl/20261009_world_bound_frozen_original_modes.json'
    mode_raw = mode_path.read_bytes(); mode_doc = json.loads(mode_raw)
    modes = {(c['id'], x['choice']): x['mode'] for c in mode_doc['cards'] for x in c['choices']}
    counters, runs, examples, pending_results = collections.Counter(), {}, {}, collections.Counter()
    modes_count, bigrams, chain_examples = collections.Counter(), collections.Counter(), []
    phase_kind, modifiers = collections.Counter(), collections.Counter()
    retained, skipped = [], []
    zips = {}
    try:
        for n, entry in enumerate(sources):
            if entry.get('local'):
                raw = (ROOT/entry['path']).read_bytes()
            else:
                volume = entry['volume']
                if volume not in zips:
                    # One open volume at a time; no extraction or large buffers.
                    for z in zips.values(): z.close()
                    zips.clear(); zips[volume] = zipfile.ZipFile(args.backup/volume)
                raw = zips[volume].read(entry['path'])
            if digest(raw) != entry['sha256']:
                raise ValueError('Source SHA mismatch: '+entry['path'])
            doc = json.loads(raw)
            trace = doc.get('trace')
            if not isinstance(trace, list) or not trace:
                skipped.append(dict(path=entry['path'], reason='empty/missing trace')); continue
            cohort = '/'.join(Path(entry['path']).parts[2:-1]).replace('/rollouts', '')
            stat = runs.setdefault(cohort, dict(episodes=0, steps=0, audit_error_episodes=0, kinds=collections.Counter()))
            stat['episodes'] += 1; stat['steps'] += len(trace)
            errors = doc.get('report', {}).get('errors', [])
            stat['audit_error_episodes'] += bool(errors)
            retained.append(dict(**entry, cohort=cohort, steps=len(trace), audit_errors=len(errors)))
            pending = None
            previous = None
            for i, row in enumerate(trace):
                action, b, a = row['action'], row['before'], row['after']
                k = action['Kind']; counters[k] += 1; stat['kinds'][k] += 1
                phase_kind[(b.get('phase','unknown'), k)] += 1
                if previous is not None: bigrams[(previous,k)] += 1
                previous = k
                if action.get('Input'): modifiers['explicit_input'] += 1
                if action.get('Targets',0): modifiers['nonzero_targets'] += 1
                if action.get('Parts',0): modifiers['nonzero_parts'] += 1
                if k not in examples:
                    examples[k] = dict(path=entry['path'], volume=entry.get('volume'), step=i,
                                       action=action, before_phase=b.get('phase'), after_phase=a.get('phase'))
                mode = None
                if k=='play':
                    mode = modes.get((action.get('Card'),action.get('Choice')), 'unknown')
                    if b.get('nextEnhanced') and action.get('Card','').startswith(('basic_card_', 'advanced_card_')):
                        mode='strong'
                    modes_count[(mode,'physical_commit' if physical_play(row) else 'unverified_commit')] += 1
                if pending:
                    if k in ('play','unit','skill','artifact') and any(
                            b.get('tokens',{}).get(c,0)>a.get('tokens',{}).get(c,0)
                            for c in b.get('tokens',{})):
                        outcome = 'source_to_strong_play' if k=='play' and mode=='strong' and physical_play(row) else 'source_to_other_token_consumer'
                        pending_results[outcome] += 1
                        pending_results['consumer_rule_steps_total'] += i-pending['step']+1
                        if len(chain_examples)<30:
                            chain_examples.append(dict(path=entry['path'], volume=entry.get('volume'),
                                start=pending['step'], end=i, outcome=outcome,
                                actions=[t['action'] for t in trace[pending['step']:i+1]],
                                caveat='same-turn temporal token decrease; not exact token provenance'))
                        pending=None
                    elif a.get('turns')!=pending['turn'] or k in ('nextTurn','rest','roundFinish'):
                        pending_results['no_observed_consumer_before_turn_boundary'] += 1; pending=None
                if k=='source':
                    if pending: pending_results['superseded_pending_source'] += 1
                    pending=dict(step=i, turn=a.get('turns'))
            if pending: pending_results['trace_ended_before_observed_consumer'] += 1
            if n%500==0: print(json.dumps(dict(processed=n+1,total=len(sources),steps=sum(counters.values()))),flush=True)
    finally:
        for z in zips.values(): z.close()
    total=sum(counters.values())
    families=collections.Counter()
    for k,count in counters.items(): families[group(k)]+=count
    # Current static route in battle: menu -> tools -> source die -> take -> close.
    # Positions are current source-defined button centres at 1920x1080. Y inversion
    # does not affect distance; hand/control retention and raycast remain unverified.
    points=[(1775,74),(1525,367.5),(774,302.5),(867.5,528),(922,966)]
    source_distance=sum(math.dist(x,y) for x,y in zip(points,points[1:]))
    baseline_path=ROOT/'AutomationOutputs/UiFluency/20261012_baseline/metrics.json'
    baseline=json.loads(baseline_path.read_text(encoding='utf-8')) if baseline_path.exists() else None
    result=dict(scope='historical training-like completed episode traces; per-cohort counts, not human frequency or policy ability',
        inventory_sha256=digest(manifest_bytes), mode_catalog=dict(path=str(mode_path),sha256=digest(mode_raw),
            caveat='historical native mode metadata; no current-rule replay or all-run version-equivalence claim'),
        candidate_archive_exports=len(archive), duplicate_byte_exports=len(duplicates),
        episodes=len(retained), steps=total, skipped=skipped, local_new_sources=len(locals_),
        action_counts=dict(counters.most_common()), family_counts=dict(families.most_common()),
        runs=runs, play_modes={str(k):v for k,v in modes_count.items()}, input_modifiers=dict(modifiers),
        source_consumer_chains=dict(pending_results), source_chain_examples=chain_examples,
        examples=examples, ui_costs=dict(source_battle=dict(clicks=5,panel_transitions=3,
            within_route_center_distance_pixels=source_distance,points=points,evidence='current_CSharp_route_estimate',
            constraints='menu initially closed, normal source allowance, one basic-color die in first row; no card targeting/material/hand navigation included'),
            unknown_action_cost=None,rule_action_is_click=False),
        limitations=['Exact-byte dedup can collapse independently identical traces; counts are conservative unique evidence.',
            'Filename selection includes smoke/integration strata; they are retained separately, not a single trained population.',
            'Audit-error episodes retained and flagged; not called valid successful gameplay.',
            'Only archived complete trace fields used; partial live rollouts and curriculum_prefix excluded.',
            'Source->consumer chains are temporal/token-decrease evidence, not exact source-token provenance.',
            'No UI distance or panel count imputed from rule steps. Unknown costs remain null.'])
    (args.out/'statistics.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    with (args.out/'sources.jsonl').open('w',encoding='utf-8') as f:
        for r in retained:f.write(json.dumps(r,ensure_ascii=False)+'\n')
    for filename,header,rows in (
        ('action_distribution.csv',['kind','family','count','share','click_cost','panel_cost','distance_px'],
            [[k,group(k),v,v/total,None,None,None] for k,v in counters.most_common()]),
        ('phase_actions.csv',['phase','kind','count'],[[*k,v] for k,v in phase_kind.most_common()]),
        ('action_transitions.csv',['from','to','count'],[[*k,v] for k,v in bigrams.most_common()])):
        with (args.out/filename).open('w',newline='',encoding='utf-8-sig') as f:
            w=csv.writer(f);w.writerow(header);w.writerows(rows)
    print(json.dumps(dict(episodes=len(retained),steps=total,actions=counters.most_common(20),chains=dict(pending_results)),ensure_ascii=False),flush=True)


if __name__=='__main__':main()
