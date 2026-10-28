"""Certify a narrow success witness against the real frozen rules, not PPO scores.

This first gate supports legacy-seed tasks. It is not the independently factored
task generator or an impossibility solver. Unknown and unproved impossible tasks
cannot enter a victory-training pool. Witness actions never become teacher data.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
from original_data_signature import data_signature

GOAL = "at_least_one_formally_completed_enemy_group"
SEQUENCE_GOAL = "complete_declared_enemy_sequence"
CHECKER_VERSION = 3


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"),
                                    ensure_ascii=False).encode("utf-8")).hexdigest()


def file_hash(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def certify_witness(rules, witness_path, dll_path):
    """Execute every actual operation through the first formal group win.

    An existence certificate for this exact initial instance is deliberately
    weaker than online stochastic solvability or winning the whole journey.
    """
    witness_path = Path(witness_path).resolve()
    document = json.loads(witness_path.read_text(encoding="utf-8-sig"))
    seed = document["seed"]
    if type(seed) is not int or not document.get("trace"):
        raise ValueError("A witness needs an integer seed and actual operations")
    definition = document.get('task_definition')
    goal = document.get('goal', SEQUENCE_GOAL if definition is not None else GOAL)
    if goal not in (GOAL, SEQUENCE_GOAL) or (goal == SEQUENCE_GOAL and definition is None):
        raise ValueError('Goal needs a supported formally bounded task definition')
    if definition is not None and definition.get('RandomSeed') != seed:
        raise ValueError('Witness random seed differs from its explicit task')
    current = rules.reset(seed) if definition is None else rules.reset_task(definition)
    original_data=data_signature()
    initial = current["state"]
    actions = {action["Key"]: i for i, action in enumerate(rules.actions)}
    executed, formal_wins = [], 0
    for step, row in enumerate(document["trace"]):
        if current["state"] != row["before"]:
            raise ValueError(f"Witness before-state mismatch at operation {step}")
        action = row["action"]
        index = actions.get(action["Key"])
        if index is None or rules.actions[index] != action:
            raise ValueError(f"Original complete action definition mismatch at {step}")
        if index not in current["legal"]:
            raise ValueError(f"Witness action is formally illegal at {step}")
        next_state = rules.step(index)
        if next_state["state"] != row["after"]:
            raise ValueError(f"Witness after-state mismatch at operation {step}")
        if next_state['reward']!=row['reward']:
            raise ValueError(f'Witness raw reward mismatch at operation {step}')
        executed.append(action)
        before, after = current["state"], next_state["state"]
        formal_win = (after["phase"] == "End" and after["won"]
                      and not before["won"])
        if formal_win:
            if not after["enemies"] or not all(e["defeated"] for e in after["enemies"]):
                raise ValueError("Formal win must contain an entirely defeated enemy group")
            formal_wins += 1
        completed_sequence = (goal == SEQUENCE_GOAL and next_state['done']
                              and after.get('stopReason') == 'task_sequence_complete'
                              and after.get('taskProgress', {}).get('sequenceCompleted')
                              and formal_wins == len(definition['EnemyGroups']))
        if (goal == GOAL and formal_win) or completed_sequence:
            if data_signature()!=original_data:
                raise ValueError('Runtime JSON inputs changed while certifying')
            binding = {"generator": "legacy_seed", "seed": seed,
                       "rules_sha256": file_hash(dll_path),
                       "feature_schema": rules.info["featureSchema"],
                       "catalog_sha256": digest(rules.actions),
                       "initial_state_sha256": digest(initial), "goal": goal}
            if definition is not None:
                binding['generator'] = 'independent_original_factors_v1'
                binding['definition'] = definition
            return {"schema_version": 1, "checker_version": CHECKER_VERSION,
                    "data_signature":original_data,
                    "checker_sha256": file_hash(__file__),
                    "task_id": digest(binding), "task": binding,
                    "status": "solvable_witness", "hero": initial["hero"],
                    "inventory_family": initial["setup"],
                    "initial_enemies": [e["id"] for e in initial["enemies"]],
                    "evidence": {"path": str(witness_path),
                                 "sha256": file_hash(witness_path),
                                 "operations": len(executed),
                                 "actions_sha256": digest(executed)},
                    "success": {"phase": "End" if goal == GOAL else 'task_sequence_complete',
                                "all_targets_defeated": True},
                    "scope_gaps": ["whole_journey_success", "online_random_outcome_success",
                                   "Unity_UI_acceptance"] + ([] if definition is not None else ['independent_task_factors'])}
        if next_state["done"]:
            break
        current = next_state
    raise ValueError(("No formal group victory" if goal == GOAL else "No completed declared sequence")
                     + ": unresolved, never proved impossible")


def eligible(certificate, dll_path, goal=None):
    """Fail closed on stale rules, edited evidence, or an unsupported proof."""
    try:
        task = certificate["task"]
        evidence = certificate["evidence"]
        expected_goal = task['goal'] if goal is None else goal
        expected_phase = 'End' if expected_goal == GOAL else 'task_sequence_complete'
        source = json.loads(Path(evidence['path']).read_text(encoding='utf-8-sig'))
        definition = source.get('task_definition')
        source_goal = source.get('goal', SEQUENCE_GOAL if definition is not None else GOAL)
        length = evidence['operations']
        if not 0 < length <= len(source['trace']) or source_goal != expected_goal:
            return False
        prefix = source['trace'][:length]
        final = prefix[-1]['after']
        if expected_goal == SEQUENCE_GOAL:
            progress = final.get('taskProgress', {})
            valid_success = (definition is not None and task.get('definition') == definition
                             and final.get('stopReason') == 'task_sequence_complete'
                             and progress.get('sequenceCompleted')
                             and progress.get('encounterIndex') == len(definition['EnemyGroups'])
                             and sum(x['after']['phase']=='End' and x['after']['won']
                                     and not x['before']['won'] for x in prefix) == len(definition['EnemyGroups']))
        else:
            valid_success = (final['phase']=='End' and final['won']
                             and not prefix[-1]['before']['won']
                             and all(e['defeated'] for e in final['enemies']))
        return (certificate["status"] == "solvable_witness"
                and certificate['data_signature']==data_signature()
                and certificate["schema_version"] == 1
                and certificate["checker_version"] == CHECKER_VERSION
                and certificate["checker_sha256"] == file_hash(__file__)
                and expected_goal in (GOAL, SEQUENCE_GOAL) and task["goal"] == expected_goal
                and task["rules_sha256"] == file_hash(dll_path)
                and certificate["task_id"] == digest(task)
                and evidence["sha256"] == file_hash(evidence["path"])
                and evidence["operations"] > 0
                and source['seed'] == task['seed']
                and digest(prefix[0]['before']) == task['initial_state_sha256']
                and digest([x['action'] for x in prefix]) == evidence['actions_sha256']
                and valid_success
                and certificate["success"] == {"phase": expected_phase, "all_targets_defeated": True})
    except (KeyError, OSError, TypeError, json.JSONDecodeError):
        return False


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dll", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("witness", nargs="+")
    args = parser.parse_args()
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),
                      BATTLE_RL_PROTOCOL="--serve-journey-artifacts",
                      BATTLE_RL_NATIVE_PAYMENTS="1", BATTLE_RL_NATIVE_CONVERSIONS="1",
                      BATTLE_RL_NATIVE_ENERGY_TARGETS="1", BATTLE_RL_ORIGINAL_OPENINGS="1")
    from train_ppo import Rules
    rules = Rules()
    accepted, unresolved = [], []
    try:
        for path in args.witness:
            try:
                certificate = certify_witness(rules, path, args.dll)
                assert eligible(certificate, args.dll)
                accepted.append(certificate)
            except (ValueError, RuntimeError) as error:
                unresolved.append({"path": str(Path(path).resolve()), "status": "unknown",
                                   "reason": str(error), "excluded_from_victory_pool": True})
    finally:
        rules.close()
    goals = {c['task']['goal'] for c in accepted}
    result = {"schema_version": 1, "goal": next(iter(goals)) if len(goals) == 1 else None, "tasks": accepted,
              "quarantine": unresolved, "impossibility_solver_implemented": False,
              "scope": "Exact replayed solvability witnesses, not original-rule acceptance"}
    target = Path(args.out)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"solvable_tasks": len(accepted), "unknown_tasks": len(unresolved)}))


if __name__ == "__main__":
    main()
