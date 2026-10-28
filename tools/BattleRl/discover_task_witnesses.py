"""Budgeted best-first witness discovery, replaying real rules for every branch.

No approximate state is restored and no duplicate visible-state merge claims
exhaustiveness (hidden RNG state may differ). Beam/branch/budget limits mean an
unsuccessful search is UNKNOWN, never an impossibility proof. Oracle actions
are evidence only, not PPO samples or mandatory policy actions.
"""
import argparse
import heapq
import json
import os
from pathlib import Path
import time

from task_feasibility import SEQUENCE_GOAL, certify_witness, eligible, file_hash
from train_ppo import Rules
from discovery_cache import read_prior, same_budget_unknown
from task_feasibility import digest
from exact_replay_cursor import ExactReplayCursor


class BudgetReached(Exception):
    pass


def search_signature():
    return digest(dict(discovery=file_hash(__file__),
                       cursor=file_hash(Path(__file__).with_name('exact_replay_cursor.py'))))


def search_branches(ordered, actions, phase, limit):
    """Keep a legal phase handoff even when many card tuples rank above it."""
    handoff=next((i for i in ordered if actions[i]['Kind'] in ('advance','roundFinish')
                  or phase in ('End','Outside') and actions[i]['Kind']=='nextTurn'),None)
    rest=next((i for i in ordered if phase=='Outside' and actions[i]['Kind']=='rest'),None)
    if len(ordered)<=limit:
        return ordered[:]
    reserved=[i for i in (handoff,rest) if i is not None][:limit]
    return [i for i in ordered if i not in reserved][:limit-len(reserved)]+reserved


def phase_diverse_frontier(frontier, width):
    """Bounded diversity only; hidden state is never merged or restored."""
    ordered=sorted(frontier)
    selected, phases=[],set()
    for row in ordered:
        if row[3] not in phases:
            selected.append(row)
            phases.add(row[3])
        if len(selected)==width:
            break
    serials={r[1] for r in selected}
    for row in ordered:
        if len(selected)==width:
            break
        if row[1] not in serials:
            selected.append(row)
    heapq.heapify(selected)
    return selected


def state_score(state):
    phase = {'Outside': 0, 'Ranged': 1, 'Block': 2, 'AssignDamage': 3, 'Melee': 4, 'End': 5}
    progress = state.get('taskProgress', {}).get('encounterIndex', 0)
    attacks = min(12, sum(p['effectiveValue'] for p in state['attacks']))
    blocks = min(12, sum(p['value'] for p in state['blocks']))
    if state['enemies'] and all(e['defeated'] for e in state['enemies']):
        attacks, blocks = 0, 0  # No living target consumes these remaining pools.
    defended = sum(a['blocked'] or a['cancelled'] for e in state['enemies'] for a in e['attacks'])
    defenses = sum(5*bool(e['effectiveFortified']) + .3*len(e['effectiveResistances'])
                   + .1*(e['effectiveArmor'] or e['armor']) for e in state['enemies'] if not e['defeated'])
    return (progress * 1000 + state['kills'] * 100 + phase[state['phase']] * 2
            + attacks + blocks * .25 + defended * 3 - state['totalWounds'] * 2
            - sum(not u['ready'] for u in state['units']) * .5
            - defenses - state['planningRun'] * .2 + bool(state['activity']) * .5)


def printed_effects():
    result = {}
    for category in ('basic_card', 'advanced_card', 'magic', 'items'):
        for card in json.loads(Path('resources/text_json', category + '.json').read_text(encoding='utf-8-sig')):
            result[card['id']] = (str(card.get('base_effect', card.get('spell_top', {}).get('effect', card.get('assign_effect', '')))),
                                  str(card.get('enhanced_effect', card.get('spell_bottom', {}).get('effect', card.get('once_per_round', '')))))
    return result


def action_priority(action, state, descriptions):
    kind, phase = action['Kind'], state['phase']
    if state['enemies'] and all(e['defeated'] for e in state['enemies']) and kind in ('advance','nextTurn'):
        return 1100
    if kind == 'attack':
        return 1000 + action['Targets'].bit_count() * 10 - action['Parts'].bit_count()
    if kind in ('block', 'blockSelected'):
        return 700 - action['Parts'].bit_count()
    if kind == 'repeat':
        return 600
    if state['roundWindow'] and kind == 'roundFinish':
        return 550
    if phase == 'End' and kind == 'nextTurn':
        return 550
    if phase == 'Outside' and kind == 'fight':
        return 500
    if kind in ('unit', 'unitLeadership', 'offeredUnit'):
        return 100 if phase in ('Ranged', 'Melee', 'Block') else 10
    if kind in ('play', 'artifact', 'boxUse'):
        text = descriptions.get(action['Card'], ('', ''))[int(action['Choice'].startswith('strong') or action['Choice']=='burn')]
        appropriate = ('遠程', '远程', '攻城', '消灭', '不攻击') if phase=='Ranged' else ('格挡', '格擋', '不攻击') if phase=='Block' else ('攻击', '攻擊', '消灭') if phase=='Melee' else ('治疗', '抽取', '魔力', '魔晶')
        return 130 if any(word in text for word in appropriate) else 15
    if kind == 'sideways':
        return 110 if phase in ('Melee', 'Block') else 0
    if kind == 'source':
        return 80
    if kind in ('skill', 'skillSideways', 'skillFork', 'skillBullseye'):
        return 50
    if kind in ('advance', 'nextTurn'):
        return 30
    if kind == 'heal':
        return 90 if state['wounds'] else 0
    return 5


def discover(rules, task, dll, out, rule_step_budget=1200, branch_limit=4,
             beam_width=4, seconds=20, initial_prefix=()):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    descriptions = printed_effects()
    started = time.perf_counter()
    calls, serial, best_score, best_prefix = 0, 0, float('-inf'), ()
    initial_prefix=tuple(initial_prefix)
    frontier = [(0., serial, initial_prefix, 'verified_prefix')]
    attempted = set()
    dead_ends = []
    last_attempt = ()
    last_before = None
    cursor=ExactReplayCursor(rules,task)

    def check_budget():
        if time.perf_counter()-started>=seconds or cursor.step_calls>=rule_step_budget:
            raise BudgetReached()

    def replay(prefix):
        nonlocal calls,last_attempt,last_before
        last_attempt=prefix
        try:
            return cursor.replay(prefix,check_budget)
        finally:
            calls=cursor.step_calls
            last_before=cursor.last_before

    try:
        while frontier:
            _, _, prefix, _ = heapq.heappop(frontier)
            current = replay(prefix)
            if current['done']:
                continue
            ordered = sorted(current['legal'], key=lambda i: (
                action_priority(rules.actions[i], current['state'], descriptions), -i), reverse=True)
            for action in search_branches(ordered,rules.actions,current['state']['phase'],branch_limit):
                child_prefix = prefix + (action,)
                if child_prefix in attempted:
                    continue
                attempted.add(child_prefix)
                child = replay(child_prefix)
                score = state_score(child['state']) - len(child_prefix) * .15
                if score > best_score:
                    best_score, best_prefix = score, child_prefix
                if child['state'].get('taskProgress', {}).get('sequenceCompleted'):
                    # Independently regenerate and certify the full exact witness.
                    s = rules.reset_task(task)
                    trace = []
                    for index in child_prefix:
                        n = rules.step(index)
                        trace.append(dict(before=s['state'], action=rules.actions[index], after=n['state'], reward=n['reward']))
                        s = n
                    witness = out / 'witness.json'
                    witness.write_text(json.dumps(dict(seed=task['RandomSeed'], task_definition=task,
                        goal=SEQUENCE_GOAL, trace=trace), ensure_ascii=False), encoding='utf-8')
                    certificate = certify_witness(rules, witness, dll)
                    if not eligible(certificate, dll, SEQUENCE_GOAL):
                        raise RuntimeError('Discovery witness failed the independent admission gate')
                    result = dict(status='solvable_witness', certificate=certificate, rule_steps=calls,
                                  verification_rule_steps=2*len(child_prefix),
                                  search_actions=len(child_prefix), seconds=time.perf_counter()-started,
                                  teacher_training_data=False)
                    break
                if child['done']:
                    dead_ends.append(dict(actions=[rules.actions[i]['Key'] for i in child_prefix],
                                          reason=child['state']['stopReason']))
                else:
                    serial += 1
                    phase_key=str(child['state'].get('taskProgress',{}).get('encounterIndex',0))+':'+child['state']['phase']
                    heapq.heappush(frontier, (-score, serial, child_prefix, phase_key))
                if len(frontier) > beam_width:
                    frontier = phase_diverse_frontier(frontier,beam_width)
            else:
                continue
            break
        else:
            raise BudgetReached()
    except BudgetReached:
        result = dict(status='unknown', reason='bounded_search_without_witness',
                      rule_steps=calls, seconds=time.perf_counter()-started,
                      excluded_from_victory_pool=True, impossibility_proved=False,
                      best_actions=[rules.actions[i]['Key'] for i in best_prefix],
                      native_failed_prefixes=dead_ends)
    except RuntimeError as error:
        result = dict(status='rule_or_replay_error',error=str(error),rule_steps=calls,
                      attempted_actions=[rules.actions[i]['Key'] for i in last_attempt],
                      last_actual_before=last_before,excluded_from_victory_pool=True,
                      task_definition=task,impossibility_proved=False)
    result['rules_sha256'] = file_hash(dll)
    result['task_definition'] = task
    result['initial_prefix_operations']=len(initial_prefix)
    result['initial_prefix_sha256']=digest(list(initial_prefix))
    result['search_file_sha256']=file_hash(__file__)
    result['search_code_sha256'] = search_signature()
    result['search_strategy']='phase_diverse_frontier_with_formal_handoff_branch'
    result['exact_live_prefix_reuse']=dict(reset_calls=cursor.reset_calls,
                                         reused_operations=cursor.reused_operations,
                                         scope='Protocol work saved, never additional training or coverage')
    result['budget'] = dict(rule_steps=rule_step_budget,branch_limit=branch_limit,beam_width=beam_width,seconds=seconds)
    (out / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dll', required=True)
    parser.add_argument('--drafts', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--max-tasks', type=int, default=4)
    parser.add_argument('--start-index', type=int, default=0)
    parser.add_argument('--rule-step-budget', type=int, default=1200)
    parser.add_argument('--seconds', type=float, default=20)
    parser.add_argument('--prior-pool', action='append', default=[])
    parser.add_argument('--partition', choices=('train','validation','test'), default='train')
    args = parser.parse_args()
    if args.max_tasks<=0 or args.start_index<0 or args.rule_step_budget<0 or args.seconds<0:
        parser.error('Positive task count and nonnegative discovery budgets required')
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')
    document = json.loads(Path(args.drafts).read_text())
    rows = {r['task_id']: r for r in document['candidates']}
    certificates, prior_attempts, rejected = read_prior(args.prior_pool, args.dll)
    partition_definitions = {digest(r['task_definition']) for r in rows.values() if r['split']==args.partition}
    for key in list(certificates):
        if key not in partition_definitions:
            rejected.append(dict(task_id=certificates[key]['task_id'], reason='not_current_'+args.partition+'_composition'))
            del certificates[key]
    budget = dict(rule_steps=args.rule_step_budget, branch_limit=4, beam_width=4, seconds=args.seconds)
    output = Path(args.out)
    if output.exists():
        raise ValueError('Inspect existing discovery evidence instead of overwriting/restarting it')
    output.mkdir(parents=True)
    rules = Rules()
    results = []
    reused, deferred = [], []
    selected_ids = (document['selected_train_drafts'] if args.partition=='train' else
                    [r['task_id'] for r in sorted(rows.values(), key=lambda r:(r['estimated_cost'],r['task_id']))
                     if r['split']==args.partition])
    if any(rows[task_id]['split']!=args.partition for task_id in selected_ids):
        raise ValueError('Discovery selection crosses declared composition split')
    try:
        for task_id in selected_ids[args.start_index:]:
            task = rows[task_id]['task_definition']
            if digest(task) in certificates:
                reused.append(task_id)
                continue
            if same_budget_unknown(prior_attempts, task, args.dll, search_signature(), budget):
                deferred.append(task_id)
                continue
            if len(results) >= args.max_tasks:
                break
            result = discover(rules, rows[task_id]['task_definition'], args.dll, output/task_id,
                              rule_step_budget=args.rule_step_budget, seconds=args.seconds)
            results.append(dict(draft_id=task_id, **result))
            if result['status']=='solvable_witness':
                certificates[digest(task)] = result['certificate']
            print(json.dumps(dict(task=task_id, status=result['status'], rule_steps=result['rule_steps'])), flush=True)
    finally:
        rules.close()
    pool = dict(schema_version=1, goal=SEQUENCE_GOAL,
                partition=args.partition,
                tasks=list(certificates.values()),
                quarantine=prior_attempts+[r for r in results if r['status']!='solvable_witness'],
                cache=dict(reused_drafts=reused, deferred_same_budget_unknown=deferred,
                           rejected_certificates=rejected, prior_pools=args.prior_pool),
                scope='Only replay-certified complete-task witnesses, not actual PPO coverage')
    (output/'solvable_pool.json').write_text(json.dumps(pool, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
