import unittest
import numpy as np
from encounter_floor_sampler import EncounterFloorSampler
from mixed_task_pool import MixedTaskSampler


class FloorTests(unittest.TestCase):
    def test_actual_encounter_clears_floor_without_removing_other_tasks(self):
        rows=[dict(task_id=str(i),hero='Tovak',status='solvable_witness' if i==0 else 'legal_reset_unknown',
                   task=dict(definition=dict(EnemyGroups=[[f'monster_{i:03}']]))) for i in range(3)]
        s=EncounterFloorSampler(rows,floor_mass=.25)
        s.events.update({'hero_enemy_encounter:Tovak:monster_000':1,'hero_enemy_encounter:Tovak:monster_001':1})
        p=s.probabilities();self.assertTrue(np.all(p>0));self.assertGreater(p[2],MixedTaskSampler(rows,s.state()).probabilities()[2])
        s.events['hero_enemy_encounter:Tovak:monster_002']=1
        np.testing.assert_array_equal(s.probabilities(),MixedTaskSampler(rows,s.state()).probabilities())
        restored=EncounterFloorSampler(rows,s.state(),.25);self.assertEqual(restored.state(),s.state())

    def test_invalid_floor_cannot_eliminate_global_support(self):
        for value in (-1,1,float('nan')):
            with self.assertRaises(ValueError):EncounterFloorSampler([],floor_mass=value)


if __name__=='__main__':unittest.main()
