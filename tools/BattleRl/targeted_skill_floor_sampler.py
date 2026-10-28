"""Temporary train-task priority for declared missing owned skills, not actions."""
import numpy as np
from skill_floor_sampler import SkillFloorSampler, observed_skill_ids
from encounter_floor_sampler import EncounterFloorSampler


def eligible_target(task, skill):
    build = task.get('Build', {})
    if skill not in build.get('SkillIds', []):
        return False
    if skill in ('hero_skill_pool_005:05', 'hero_skill_pool_002:07'):
        if not task.get('OriginalWorld', False):
            return False
        if skill == 'hero_skill_pool_002:07' and len(build.get('UnitIds', [])) >= 3:
            # This priority is for direct recruitment opportunities in the
            # current three-slot observer. These roots still retain global
            # probability and can formally dismiss a unit later.
            return False
    return True


class TargetedSkillFloorSampler(SkillFloorSampler):
    def __init__(self,certificates,state=None,floor_mass=.25,skill_floor_mass=.25,targets=()):
        super().__init__(certificates,state,floor_mass,skill_floor_mass)
        self.targets=tuple(sorted(set(targets)))
        available={s for row in certificates for s in row['task']['definition'].get('Build',{}).get('SkillIds',[])}
        if not self.targets or not set(self.targets)<=available:
            raise ValueError('Targets must be real owned skills in the existing train pool')

    def probabilities(self):
        seen=observed_skill_ids(self.events)
        remaining=set(self.targets)-seen
        if not remaining:return super().probabilities()
        base=EncounterFloorSampler.probabilities(self)
        selected=np.array([any(eligible_target(row['task']['definition'],skill) for skill in remaining)
                           for row in self.certificates])
        focal=np.array([row.get('world_consumer_priority',False) for row in self.certificates],dtype=bool)&selected
        if focal.any():selected=focal
        if not selected.any():return base
        return (1-self.skill_floor_mass)*base+self.skill_floor_mass*selected/selected.sum()

    def state(self):
        result=super().state()
        result.update(sampling_kind='global_mix_targeted_actual_skill_floor_v1',skill_floor_targets=list(self.targets))
        return result
