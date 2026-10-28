"""Actual owned-skill deficits guide task probability, never policy actions."""
import math
import numpy as np
from encounter_floor_sampler import EncounterFloorSampler


def observed_skill_ids(events):
    """Priority can finish on a real passive consumer, without calling it activation."""
    result = set()
    for key, count in events.items():
        if count <= 0:
            continue
        for prefix in ('skill_invocation:', 'skill_consumer:'):
            if key.startswith(prefix):
                result.add(key[len(prefix):].rsplit(':', 1)[0])
    return result


class SkillFloorSampler(EncounterFloorSampler):
    def __init__(self,certificates,state=None,floor_mass=.25,skill_floor_mass=.25):
        if not math.isfinite(skill_floor_mass) or not 0<=skill_floor_mass<1:
            raise ValueError('Skill floor must preserve positive global task support')
        super().__init__(certificates,state,floor_mass)
        self.skill_floor_mass=float(skill_floor_mass)

    def probabilities(self):
        base=super().probabilities()
        seen=observed_skill_ids(self.events)
        deficit=np.array([any(skill not in seen for skill in row['task']['definition'].get('Build',{}).get('SkillIds',[]))
                          for row in self.certificates])
        if not deficit.any():return base
        return (1-self.skill_floor_mass)*base+self.skill_floor_mass*deficit/deficit.sum()

    def state(self):
        result=super().state()
        result.update(sampling_kind='global_mix_actual_skill_floor_v1',skill_floor_mass=self.skill_floor_mass)
        return result
