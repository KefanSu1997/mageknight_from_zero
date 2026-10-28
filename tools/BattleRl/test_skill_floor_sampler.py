import unittest
import numpy as np
from skill_floor_sampler import SkillFloorSampler
from encounter_floor_sampler import EncounterFloorSampler


class SkillFloorTests(unittest.TestCase):
    def test_actual_invocation_clears_floor_not_ownership_or_selection(self):
        rows=[dict(task_id=str(i),hero='Tovak',status='solvable_witness' if i==0 else 'legal_reset_unknown',
                   task=dict(definition=dict(EnemyGroups=[['monster_001']],Build=dict(SkillIds=['hero_skill_pool_007:09'] if i==1 else [])))) for i in range(3)]
        sampler=SkillFloorSampler(rows)
        sampler.events['hero_enemy_encounter:Tovak:monster_001']=1
        base=EncounterFloorSampler(rows,sampler.state()).probabilities()
        p=sampler.probabilities();self.assertTrue(np.all(p>0));self.assertGreater(p[1],base[1])
        sampler.events['skill_selection:hero_skill_pool_007:09']=20
        np.testing.assert_array_equal(p,sampler.probabilities())
        sampler.events['skill_invocation:hero_skill_pool_007:09:3']=1
        np.testing.assert_array_equal(base,sampler.probabilities())
        self.assertEqual(SkillFloorSampler(rows,sampler.state()).state(),sampler.state())

    def test_invalid_mass_cannot_exclude_global_tasks(self):
        for value in (-1,1,float('nan')):
            with self.assertRaises(ValueError):SkillFloorSampler([],skill_floor_mass=value)


if __name__=='__main__':unittest.main()
