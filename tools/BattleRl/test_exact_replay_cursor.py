import json
import os
from pathlib import Path
import unittest
from exact_replay_cursor import ExactReplayCursor
from train_ppo import Rules

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class ExactCursorTests(unittest.TestCase):
    def test_rejected_action_keeps_actual_before_state(self):
        witness=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_original_tovak_handoff_witness/witness.json').read_text())
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        rules=Rules()
        try:
            cursor=ExactReplayCursor(rules,witness['task_definition'])
            fight=next(i for i,a in enumerate(rules.actions) if a['Kind']=='fight')
            before=cursor.replay((fight,))
            with self.assertRaises(RuntimeError):
                cursor.replay((fight,10**9))
            self.assertEqual(cursor.current,before)
            self.assertEqual(cursor.last_before,before['state'])
            self.assertEqual(cursor.step_calls,1)
        finally:
            rules.close()

    def test_two_battles_units_and_source_match_full_replay(self):
        pool=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_discovery_two_groups/solvable_pool.json').read_text())
        certificate=next(c for c in pool['tasks'] if len(c['task']['definition']['EnemyGroups'])==2)
        witness=json.loads(Path(certificate['evidence']['path']).read_text())
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        optimized,reference=Rules(),Rules()
        try:
            indices={a['Key']:i for i,a in enumerate(optimized.actions)}
            path=tuple(indices[r['action']['Key']] for r in witness['trace'])
            cursor=ExactReplayCursor(optimized,witness['task_definition'])
            full_steps=0
            for length in (3,6,9,12,15,len(path),4,len(path)):
                prefix=path[:length]
                expected=reference.reset_task(witness['task_definition'])
                for index in prefix:
                    expected=reference.step(index)
                    full_steps+=1
                self.assertEqual(cursor.replay(prefix),expected)
            self.assertTrue(expected['state']['taskProgress']['sequenceCompleted'])
            report=dict(status='passed',protocol_comparisons=8,actual_cursor_steps=cursor.step_calls,
                        full_root_steps=full_steps,reused_operations=cursor.reused_operations,
                        scope='Two real battles, units/source, complete state/features/legal/reward/done matched')
            (ROOT/'tmp/rl_exact_cursor_two_battle_equivalence.json').write_text(json.dumps(report,indent=2))
            self.assertLess(cursor.step_calls,full_steps)
        finally:
            optimized.close()
            reference.close()

    def test_real_progress_rewind_and_sibling_match_full_root_replay(self):
        witness=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_original_tovak_handoff_witness/witness.json').read_text())
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        optimized,reference=Rules(),Rules()
        try:
            indices={a['Key']:i for i,a in enumerate(optimized.actions)}
            path=tuple(indices[r['action']['Key']] for r in witness['trace'])
            cursor=ExactReplayCursor(optimized,witness['task_definition'])
            # Extend, equal prefix, rewind and sibling branch; compare whole
            # actual protocol including every public feature/full legal mask.
            paths=[(),path[:1],path[:4],path[:4],path[:2],path[:7],path]
            naive_steps=0
            for prefix in paths:
                expected=reference.reset_task(witness['task_definition'])
                for index in prefix:
                    expected=reference.step(index)
                    naive_steps+=1
                self.assertEqual(cursor.replay(prefix),expected)
            self.assertLess(cursor.step_calls,naive_steps)
            self.assertGreater(cursor.reused_operations,0)
            sibling_root=cursor.replay(path[:4])
            alternative=next(i for i in sibling_root['legal'] if i!=path[4])
            target=path[:4]+(alternative,)
            expected=reference.reset_task(witness['task_definition'])
            for index in target:
                expected=reference.step(index)
            self.assertEqual(cursor.replay(target),expected)
        finally:
            optimized.close()
            reference.close()


if __name__=='__main__':
    unittest.main()
