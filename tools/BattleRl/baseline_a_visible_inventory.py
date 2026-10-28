"""Uncapped visible card/skill/offer slots in A's existing entity layout.

No learned DTO tree or identity hashing. Remaining unit/combat/public-obligation
adapters must be added before this is used as a complete scenario input.
"""
import numpy as np
from public_entity_tokens import KINDS

CARD_ZONES=('Hand','Discard','Played','RoundAside')
OFFER_ZONES=('Units','Spells','AdvancedActions','CommonSkills','LevelSkills',
             'AdventureArtifacts','RuinArtifacts','CityArtifacts','MonasteryActions')
VISIBLE_ROLES=tuple('inventory_'+x for x in CARD_ZONES)+('inventory_Skills',)+tuple('offer_'+x for x in OFFER_ZONES)
CARD_TYPES=('Action','Wound','Skill','Artifact','Spell')  # native Core.CardType


class VisibleInventoryCodec:
    def __init__(self,original_ids,role_offset):
        self.ids={identity:i for i,identity in enumerate(original_ids)}
        self.role_offset=role_offset

    def encode(self,observation):
        output=[]
        def add(role,slot,identity,values,wound=False):
            if identity not in self.ids and not wound:raise ValueError('Missing printed entity '+str(identity))
            attributes=np.zeros(54,np.float32)
            # Unlike legacy six-slot onehot this exact scalar retains every
            # visible slot; no clipping or dropping slot 6 and beyond.
            attributes[0]=slot/32
            attributes[1:1+len(values)]=values
            # Native PlayerDeck creates wounds as id "w". They have no entry
            # in A's printed-rule cache: retain the explicit Wound attribute,
            # use its declared blank row, never alias them to an action card.
            output.append((len(self.ids) if wound else self.ids[identity],self.role_offset+VISIBLE_ROLES.index(role),attributes))
        for zone in CARD_ZONES:
            for index,card in enumerate(observation['Inventory'][zone]):
                if set(card)!={'Slot','Id','Type'} or card['Slot']!=index:raise ValueError('Visible card schema/order changed')
                if card['Type'] not in CARD_TYPES:raise ValueError('Unknown printed card type '+card['Type'])
                wound=card['Id']=='w' and card['Type']=='Wound'
                add('inventory_'+zone,index,card['Id'],[float(card['Type']==t) for t in CARD_TYPES],wound=wound)
        for index,skill in enumerate(observation['Inventory']['Skills']):
            if set(skill)!={'Slot','Id','UsedThisTurn','UsedThisRound','Flipped'} or skill['Slot']!=index:
                raise ValueError('Visible skill schema/order changed')
            add('inventory_Skills',index,skill['Id'],[skill['UsedThisTurn'],skill['UsedThisRound'],skill['Flipped']])
        for zone in OFFER_ZONES:
            for index,card in enumerate(observation['Offers'][zone]):
                if set(card)!={'Slot','Id'} or card['Slot']!=index:raise ValueError('Offer schema/order changed')
                add('offer_'+zone,index,card['Id'],[])
        return output
