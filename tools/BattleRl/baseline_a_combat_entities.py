"""All actual combat participants/contributions as A-style flat entities."""
import re
from pathlib import Path
import numpy as np

COMBAT_ROLES=('combat_status','combat_enemy','combat_enemy_attack','combat_attack',
              'combat_attack_exclusion','combat_attack_card','combat_block')


class CombatEntityCodec:
    def __init__(self,repository,original_ids,role_offset):
        self.ids={x:i for i,x in enumerate(original_ids)};self.blank=len(original_ids);self.offset=role_offset
        source=(Path(repository)/'Assets/Logic/Core/Enum.cs').read_text(encoding='utf-8-sig')
        def enum(name):
            body=re.search(r'\benum\s+'+name+r'\s*\{([^}]*)}',source).group(1)
            body=re.sub(r'//[^\n]*|/\*.*?\*/','',body,flags=re.S)
            return tuple(x.strip().split('=')[0].strip() for x in body.split(',') if x.strip())
        self.elements=enum('Element');self.types=enum('AttackType');self.abilities=enum('Ability');self.phases=('Outside',)+enum('Phase')

    def encode(self,combat):
        result=[]
        def require(item,fields):
            if set(item)!=set(fields.split()):raise ValueError('Combat schema changed: '+str(set(item)^set(fields.split())))
        def category(value,values):
            if value not in values:raise ValueError('Unknown combat category '+str(value))
            return [float(value==x) for x in values]
        def abilities(values):
            if set(values)-set(self.abilities):raise ValueError('Unknown enemy ability')
            return [float(x in values) for x in self.abilities]
        def add(role,values,identity=None):
            if len(values)>54:raise ValueError('Combat attributes overflow; no truncation')
            if identity is not None and identity not in self.ids:raise ValueError('Unknown combat printed ID '+identity)
            a=np.zeros(54,np.float32);a[:len(values)]=values
            if not np.isfinite(a).all():raise ValueError('Nonfinite combat attribute')
            result.append((self.blank if identity is None else self.ids[identity],self.offset+COMBAT_ROLES.index(role),a))
        require(combat,'Active Phase Enemies Attacks Blocks AttackReduction AttackReductionTimes ExplosiveArmorUses MoveCostAttack MoveCostRanged InfluenceAsBlock InfluenceBlockElement')
        add('combat_status',[combat['Active'],*category(combat['Phase'],self.phases),
            *[combat[k]/32 for k in ('AttackReduction','AttackReductionTimes','ExplosiveArmorUses','MoveCostAttack','MoveCostRanged')],
            combat['InfluenceAsBlock'],*category(combat['InfluenceBlockElement'],(None,)+self.elements)])
        for i,e in enumerate(combat['Enemies']):
            require(e,'Slot Id Defeated PrintedArmor EffectiveArmor Abilities Attacks')
            if e['Slot']!=i:raise ValueError('Enemy slot order mismatch')
            add('combat_enemy',[i/32,e['Defeated'],e['PrintedArmor']/32,e['EffectiveArmor']/32,*abilities(e['Abilities'])],e['Id'])
            for j,a in enumerate(e['Attacks']):
                require(a,'Index Power Element Abilities Blocked Cancelled')
                if a['Index']!=j:raise ValueError('Enemy attack order mismatch')
                add('combat_enemy_attack',[i/32,j/32,a['Power']/32,*category(a['Element'],self.elements),
                    a['Blocked'],a['Cancelled'],*abilities(a['Abilities'])],e['Id'])
        for i,a in enumerate(combat['Attacks']):
            require(a,'Slot Value Element Type Available Sweep RequiredEnemy ExcludedEnemies UnitSlot Skill StarBowChoice FamePerKill ReputationPerKill FameIfKill CrystalsPerKill ArmorReductionsPerKill SourceCardId PlayedCardSlot')
            if a['Slot']!=i:raise ValueError('Attack slot order mismatch')
            add('combat_attack',[i/32,a['Value']/32,*category(a['Element'],self.elements),*category(a['Type'],self.types),
                a['Available'],a['Sweep'],*[a[k]/32 for k in ('RequiredEnemy','UnitSlot','StarBowChoice','FamePerKill',
                    'ReputationPerKill','FameIfKill','CrystalsPerKill','ArmorReductionsPerKill','PlayedCardSlot')]],a['Skill'])
            if a['SourceCardId'] is not None:add('combat_attack_card',[i/32,a['PlayedCardSlot']/32],a['SourceCardId'])
            for excluded in a['ExcludedEnemies']:add('combat_attack_exclusion',[i/32,excluded/32])
        for i,b in enumerate(combat['Blocks']):
            require(b,'Slot Value Element RequiredEnemy RequiredAttack Target Attack UnitSlot Skill CountSymbols ArmorReduction AttackOnSuccess KillOnSuccess DistinctGroup DoubleSwift ReduceByExcess')
            if b['Slot']!=i:raise ValueError('Block slot order mismatch')
            add('combat_block',[i/32,b['Value']/32,*category(b['Element'],self.elements),
                *[b[k]/32 for k in ('RequiredEnemy','RequiredAttack','Target','Attack','UnitSlot','ArmorReduction','AttackOnSuccess','DistinctGroup')],
                *[b[k] for k in ('CountSymbols','KillOnSuccess','DoubleSwift','ReduceByExcess')]],b['Skill'])
        return result
