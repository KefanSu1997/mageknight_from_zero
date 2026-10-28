"""Explicit solo unit/resource records for A's 54-attribute entity encoder."""
import re
from pathlib import Path
import numpy as np

RESOURCE_ROLES=('inventory_status','mana','source_status','source_available','source_turnheld',
 'source_roundheld','source_tacticheld','unit','unit_banner','unit_banner_effect',
 'unit_ability','unit_ability_effects','puppet_candidate','puppet_stored','puppet_attack')


class UnitResourceCodec:
    def __init__(self,repository,original_ids,role_offset):
        self.ids={x:i for i,x in enumerate(original_ids)};self.blank=len(original_ids);self.offset=role_offset
        root=Path(repository)
        source=(root/'Assets/Logic/Core/Enum.cs').read_text(encoding='utf-8-sig')
        source+='\n'+(root/'Assets/Logic/Data/UnitAbility.cs').read_text(encoding='utf-8-sig')
        def enum(name):
            body=re.search(r'\benum\s+'+name+r'\s*\{([^}]*)}',source).group(1)
            body=re.sub(r'//[^\n]*|/\*.*?\*/','',body,flags=re.S)
            return tuple(x.strip().split('=')[0].strip() for x in body.split(',') if x.strip())
        self.colors=enum('ManaColor');self.elements=enum('Element');self.attacks=enum('AttackType')
        self.kinds=enum('UnitAbilityKind');self.terrains=enum('TerrainType');self.abilities=enum('Ability')

    def encode(self,inventory):
        output=[]
        def exact(d,fields):
            if set(d)!=set(fields.split()):raise ValueError('Resource DTO changed '+str(set(d)^set(fields.split())))
        def cat(value,vocab):
            if value not in vocab:raise ValueError('Unknown resource category '+str(value))
            return [float(value==v) for v in vocab]
        def enumcat(value,vocab):
            if type(value) is not int or not 0<=value<len(vocab):raise ValueError('Invalid native enum')
            return [float(value==i) for i in range(len(vocab))]
        def optionalenum(value,vocab):return [float(value is None)]+([0.]*len(vocab) if value is None else enumcat(value,vocab))
        def add(role,values,identity=None):
            if identity is not None and identity not in self.ids:raise ValueError('Unknown printed resource '+identity)
            if len(values)>54:raise ValueError('Resource attribute overflow')
            a=np.zeros(54,np.float32);a[:len(values)]=values
            if not np.isfinite(a).all():raise ValueError('Nonfinite resource')
            output.append((self.blank if identity is None else self.ids[identity],self.offset+RESOURCE_ROLES.index(role),a))
        # Hero identity remains in original A numeric input. No new opaque hero hash.
        add('inventory_status',[inventory[k]/32 for k in ('Fame','Level','Reputation','Armor','CommandSlots','DrawCount',
            'TacticAsideCount','Movement','Influence','Healing','GrailHealing')]+[inventory['ForgoSourceDice']])
        for m in inventory['Mana']:
            exact(m,'Color Tokens Crystals');add('mana',cat(m['Color'],self.colors)+[m['Tokens']/32,m['Crystals']/32])
        source=inventory['Source']
        add('source_status',[source is not None]+([] if source is None else [source['UsedThisTurn'],source['ExtraDice']/32,source['ExtraBlackDice']/32]))
        if source is not None:
            exact(source,'Available TurnHeld RoundHeld TacticHeld UsedThisTurn ExtraDice ExtraBlackDice')
            for zone in ('Available','TurnHeld','RoundHeld','TacticHeld'):
                for i,d in enumerate(source[zone]):
                    exact(d,'Slot Face OwnedByPlayer PendingReturnChoice')
                    if d['Slot']!=i:raise ValueError('Source slot mismatch')
                    add('source_'+zone.lower(),[i/32,*cat(d['Face'],self.colors),d['OwnedByPlayer'],d['PendingReturnChoice']])
        for i,u in enumerate(inventory['Units']):
            exact(u,'Slot Id Ready CanActivate Wounds Destroyed Banner BannerEffect Attunement NeedsUpkeep CourageSpent FortitudeSpent ReturningBanner Actions')
            if u['Slot']!=i:raise ValueError('Unit slot mismatch')
            add('unit',[i/32,u['Ready'],u['CanActivate'],u['Wounds']/32,u['Destroyed'],u['NeedsUpkeep'],u['CourageSpent'],
                u['FortitudeSpent'],u['ReturningBanner'],*cat(u['Attunement'],(None,)+self.colors)],u['Id'])
            for key,role in (('Banner','unit_banner'),('BannerEffect','unit_banner_effect')):
                if u[key] is not None:add(role,[i/32],u[key])
            for j,ability in enumerate(u['Actions']):
                exact(ability,'Index Kind Definition');d=ability['Definition']
                if ability['Index']!=j:raise ValueError('Unit ability slot mismatch')
                exact(d,'PrintedRule Kind Value Element AttackType Cost SelfWounds ReputationChange DoubleAgainstSwift GrantedMana RedBlueBoost MovementReductionTerrains MovementReduction RemoveTargetFortifications RemoveTargetResistances InteractionReputationChange OtherUnitAttackBonus AttunementColor AttunementBonus ExplorationRange MovementPhaseOnly')
                if self.kinds[d['Kind']]!=ability['Kind']:raise ValueError('Unit ability kind mismatch')
                cost=[(d['Cost'] or []).count(k)/8 for k in range(len(self.colors))]
                if any(type(k)is not int or not 0<=k<len(self.colors) for k in (d['Cost'] or [])):raise ValueError('Unknown cost')
                # PrintedRule is static card text, already bound by the unit's
                # original rule vector; ability index connects each numeric effect.
                add('unit_ability',[i/32,j/32,*enumcat(d['Kind'],self.kinds),d['Value']/32,
                    *enumcat(d['Element'],self.elements),*enumcat(d['AttackType'],self.attacks),*cost],u['Id'])
                terrain=d['MovementReductionTerrains'] or []
                if any(type(k)is not int or not 0<=k<len(self.terrains) for k in terrain):raise ValueError('Unknown terrain')
                add('unit_ability_effects',[i/32,j/32,*[d[k]/32 for k in ('SelfWounds','ReputationChange','MovementReduction',
                    'InteractionReputationChange','OtherUnitAttackBonus','AttunementBonus','ExplorationRange')],
                    *[d[k] for k in ('DoubleAgainstSwift','RedBlueBoost','RemoveTargetFortifications','RemoveTargetResistances','MovementPhaseOnly')],
                    *optionalenum(d['GrantedMana'],self.colors),*optionalenum(d['AttunementColor'],self.colors),
                    *[float(k in terrain) for k in range(len(self.terrains))]],u['Id'])
        for zone,role in (('PuppetCandidates','puppet_candidate'),('PuppetStored','puppet_stored')):
            for i,p in enumerate(inventory[zone]):
                exact(p,'Slot Id Armor Abilities Attacks')
                if p['Slot']!=i or set(p['Abilities'])-set(self.abilities):raise ValueError('Puppet schema mismatch')
                add(role,[i/32,p['Armor']/32,*[float(a in p['Abilities']) for a in self.abilities]],p['Id'])
                for j,a in enumerate(p['Attacks']):
                    exact(a,'Power Element');add('puppet_attack',[zone=='PuppetStored',i/32,j/32,a['Power']/32,*cat(a['Element'],self.elements)],p['Id'])
        return output
