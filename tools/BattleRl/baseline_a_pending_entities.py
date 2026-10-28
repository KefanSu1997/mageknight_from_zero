"""Explicit public reward/turn obligations; no hidden supply or future cards."""
import numpy as np
from baseline_a_visible_inventory import OFFER_ZONES

PENDING_ROLES=('offers_status','adventure_die','ruin_reward','repeated_effect','committed_end',
               'committed_discard','committed_gold','committed_ruin','committed_ruin_artifact')


class PendingEntityCodec:
    def __init__(self,ids,offset):self.ids={v:i for i,v in enumerate(ids)};self.blank=len(ids);self.offset=offset
    def encode(self,offers):
        result=[]
        def exact(d,fields):
            if set(d)!=set(fields):raise ValueError('Pending observation schema changed')
        def add(role,values,identity=None):
            if identity is not None and identity not in self.ids:raise ValueError('Unknown repeated card')
            a=np.zeros(54,np.float32)
            if len(values)>54:raise ValueError('Pending overflow')
            a[:len(values)]=values
            if not np.isfinite(a).all():raise ValueError('Nonfinite pending')
            result.append((self.blank if identity is None else self.ids[identity],self.offset+PENDING_ROLES.index(role),a))
        def enum(v,values):
            if v not in values:raise ValueError('Unknown pending enum '+str(v))
            return [float(v==x) for x in values]
        exact(offers,set(OFFER_ZONES)|set('PendingLevel PendingLevelNumber InNegotiation InteractionClosed PendingSpellReward AdventureRewardDice RuinRewards PurchasedCityArtifacts CommittedRewardEnd RepeatedEffect LearningModes LearningUsed'.split()))
        add('offers_status',[offers[k] for k in ('PendingLevel','InNegotiation','InteractionClosed','PendingSpellReward','LearningUsed')]+
            [offers[k]/32 for k in ('PendingLevelNumber','PurchasedCityArtifacts','LearningModes')])
        for i,c in enumerate(offers['AdventureRewardDice']):add('adventure_die',[i/32,*enum(c,('Red','Blue','Green','White','Gold','Black'))])
        for i,r in enumerate(offers['RuinRewards']):add('ruin_reward',[i/32,*enum(r,('Artifact','Spell','AdvancedAction','Unit','FourBasicCrystals'))])
        repeat=offers['RepeatedEffect']
        if repeat is not None:
            exact(repeat,('CardId','Enhanced','Completed','Remaining'))
            add('repeated_effect',[repeat['Enhanced'],repeat['Completed']/32,repeat['Remaining']/32],repeat['CardId'])
        end=offers['CommittedRewardEnd']
        if end is not None:
            exact(end,'DiscardHandSlots WoundZone WoundSlot MineColor SpellRewardSlot ArtifactRewardSlot GoldRewardColors RuinReward CommitRewards'.split())
            add('committed_end',[*enum(end['WoundZone'],(0,1,2)),end['WoundSlot']/32,*enum(end['MineColor'],(None,0,1,2,3,4,5)),
                end['SpellRewardSlot']/32,end['ArtifactRewardSlot']/32,end['CommitRewards']])
            for i,s in enumerate(end['DiscardHandSlots']):add('committed_discard',[i/32,s/32])
            for i,c in enumerate(end['GoldRewardColors'] or []):add('committed_gold',[i/32,*enum(c,tuple(range(6)))])
            ruin=end['RuinReward']
            if ruin is not None:
                exact(ruin,'ArtifactSlots SpellSlot AdvancedSlot UnitSlot DisbandSlot LoyaltySlot'.split())
                add('committed_ruin',[ruin[k]/32 for k in ('SpellSlot','AdvancedSlot','UnitSlot','DisbandSlot')]+[ruin['LoyaltySlot']])
                for i,s in enumerate(ruin['ArtifactSlots']):add('committed_ruin_artifact',[i/32,s/32])
        return result
