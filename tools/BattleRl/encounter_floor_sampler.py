"""Supplement global mixing with public actual-encounter coverage, never game actions."""
import math
import numpy as np
from mixed_task_pool import MixedTaskSampler


class EncounterFloorSampler(MixedTaskSampler):
    def __init__(self,certificates,state=None,floor_mass=.25):
        if not math.isfinite(floor_mass) or not 0<=floor_mass<1:
            raise ValueError('Encounter floor must retain positive global mixing mass')
        super().__init__(certificates,state)
        self.floor_mass=float(floor_mass)

    def probabilities(self):
        base=super().probabilities()
        seen={key.split(':')[-1] for key,count in self.events.items()
              if key.startswith('hero_enemy_encounter:') and count>0}
        missing=np.array([any(enemy not in seen for enemy in row['task']['definition']['EnemyGroups'][0])
                          for row in self.certificates])
        if not missing.any():return base
        return (1-self.floor_mass)*base+self.floor_mass*missing/missing.sum()

    def state(self):
        result=super().state()
        result.update(sampling_kind='global_mix_actual_encounter_floor_v1',encounter_floor_mass=self.floor_mass)
        return result
