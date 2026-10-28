"""Explicit world entities using A's 54-attribute + frozen-text representation.

Spatial entities with no printed card identity use an explicit zero text row;
their terrain/site semantics are one-hot attributes, not fabricated rule text.
Unknown actual card/enemy identities fail instead of silently using that row.
"""
import hashlib
import json
from pathlib import Path
import re
import numpy as np
from torch import nn
from atomic_entity_text import EntityTextEncoder, ATTRIBUTE_WIDTH
from public_entity_tokens import KINDS
from baseline_a_visible_inventory import VISIBLE_ROLES
from baseline_a_combat_entities import COMBAT_ROLES
from baseline_a_unit_resources import RESOURCE_ROLES
from baseline_a_pending_entities import PENDING_ROLES

WORLD_ROLES=('map_cell','map_site','map_guard','map_ruin','exploration_position',
             'scouting','scouted_ruin','dummy_discard')


class WorldEntityCodec:
    def __init__(self, repository, original_ids):
        root=Path(repository);self.ids={x:i for i,x in enumerate(original_ids)}
        self.blank_id=len(original_ids)
        paths=[root/'resources/original_maps'/n for n in ('tutorial_opening.json',
            'countryside_3_11_transcription.json','core_1_8_transcription.json')]
        cells=[c for p in paths for t in json.loads(p.read_text(encoding='utf-8-sig'))['tiles'] for c in t['cells']]
        self.terrain=tuple(sorted({c['terrain'] for c in cells}))
        self.printed=(None,)+tuple(sorted({c['site'] for c in cells if c.get('site') is not None}))
        enum_path=root/'Assets/Logic/Core/Enum.cs'
        source=enum_path.read_text(encoding='utf-8-sig')
        def enum(name):
            body=re.search(r'\benum\s+'+name+r'\s*\{([^}]*)}',source).group(1)
            body=re.sub(r'//[^\n]*|/\*.*?\*/','',body,flags=re.S)
            return tuple(x.strip().split('=')[0].strip() for x in body.split(',') if x.strip())
        self.site=enum('PlaceType');self.city=enum('CityColor')
        self.color=enum('ManaColor');self.enemy_color=enum('EnemyTokenColor')
        ruin_path=root/'resources/original_ruins/base_tokens.json'
        self.ruin_ids=tuple(t['id'] for t in json.loads(ruin_path.read_text(encoding='utf-8-sig'))['tokens'])
        self.sources={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths+[enum_path,ruin_path]}
        self.attribute_names={}

    def encode(self, records):
        encoded=[]
        queue=list(records)
        for record in queue:
            r=record;role=r['role'];names=[];values=[];identity=None
            def num(name,value,scale=1):
                if type(value) not in (int,float,bool) or not np.isfinite(value):raise ValueError('Invalid '+name)
                names.append(name);values.append(float(value)/scale)
            def optional(name,value,scale=1):
                num(name+'.present',value is not None);num(name,0 if value is None else value,scale)
            def cat(name,value,vocab):
                if value not in vocab:raise ValueError('Unknown '+name+': '+str(value))
                for c in vocab:num(name+'='+str(c),value==c)
            def counts(name,items,vocab):
                if any(x not in vocab for x in items):raise ValueError('Unknown '+name)
                for c in vocab:num(name+'='+str(c),items.count(c),4)
            if role in ('map_cell','map_site','map_guard','map_ruin','exploration_position','scouted_ruin'):
                optional('q',r['q'],32);optional('r',r['r'],32)
            if role=='map_cell':
                cat('terrain',r['terrain'],self.terrain);cat('printed_site',r['printed_site'],self.printed)
            elif role=='map_site':
                cat('type',r['Type'],self.site);num('conquered',r['Conquered'])
                optional('owner',r['OwnerId'],7);cat('city_color',r['CityColor'],(None,)+self.city)
                num('city_level',r['CityLevel'],10);counts('mine_colors',r['MineColors'],self.color)
                num('token_revealed',r['TokenRevealed']);cat('ruin_token',r['RuinToken'],(None,'Altar','Enemy'))
                num('monastery_burned',r['MonasteryBurned'])
            elif role=='map_guard':
                num('slot',r['Slot'],8);cat('back_color',r['BackColor'],self.enemy_color)
                num('revealed',r['Revealed']);identity=r['DefinitionId']
                if not r['Revealed'] and identity is not None:raise ValueError('Hidden guard')
            elif role in ('map_ruin','scouted_ruin'):
                cat('ruin_id',r['Id'],self.ruin_ids);num('fame',r['Fame'],20)
                counts('mana_cost',r['ManaCost'],self.color);counts('enemies',r['Enemies'],self.enemy_color)
                counts('rewards',r['Rewards'],('Artifact','Spell','AdvancedAction','Unit','FourBasicCrystals'))
            elif role=='exploration_position':pass
            elif role=='scouting':
                optional('q',r['Q'],32);optional('r',r['R'],32);optional('guard_slot',r['GuardSlot'],8)
                cat('enemy_color',r['EnemyColor'],(None,)+self.enemy_color)
                cat('ruin_token',r['RuinToken'],(None,'Altar','Enemy'));identity=r['EnemyId']
                num('ruin_details',r['RuinDetails'] is not None)
                if r['RuinDetails'] is not None:
                    queue.append(dict(role='scouted_ruin',q=r['Q'],r=r['R'],**r['RuinDetails']))
            elif role=='dummy_discard':
                num('index',r['index'],32)
                # Native ManaColor is an integer enum in this DTO.
                cat('color',r['color'],tuple(range(len(self.color))))
                cat('second_color',r['second_color'],(None,)+tuple(range(len(self.color))))
                identity=r['identity']
            else:raise ValueError('Unknown world entity role '+role)
            if len(values)>ATTRIBUTE_WIDTH:raise ValueError('Expand schema; never truncate '+role)
            if role in self.attribute_names and self.attribute_names[role]!=names:
                raise ValueError('World attribute schema depends on current state')
            self.attribute_names[role]=names
            if identity is not None and identity not in self.ids:raise ValueError('Missing printed text '+identity)
            attributes=np.zeros(ATTRIBUTE_WIDTH,np.float32);attributes[:len(values)]=values
            encoded.append((self.blank_id if identity is None else self.ids[identity],
                            len(KINDS)+WORLD_ROLES.index(role),attributes))
        return encoded


class ScenarioEntityTextEncoder(EntityTextEncoder):
    """Same A attention/numeric/fusion topology, explicit additional role rows."""
    def __init__(self,space,entity_vectors):
        super().__init__(space,entity_vectors)
        self.role_embedding=nn.Embedding(len(KINDS)+len(WORLD_ROLES)+len(VISIBLE_ROLES)+len(COMBAT_ROLES)+len(RESOURCE_ROLES)+len(PENDING_ROLES),128)

    def load_reference(self,state):
        own=self.state_dict()
        if set(own)!=set(state):raise ValueError('Not a baseline A encoder')
        for key,value in state.items():
            if key=='numeric.0.weight':
                if value.shape[1]!=6411 or own[key].shape[1]<6411:raise ValueError('Original numeric schema mismatch')
                own[key].zero_();own[key][:,:6411].copy_(value)
            elif key=='role_embedding.weight':
                if value.shape!=(len(KINDS),128):raise ValueError('Original role schema mismatch')
                own[key][:len(KINDS)].copy_(value)
            else:
                if own[key].shape!=value.shape:raise ValueError('Unexpected architecture change '+key)
                own[key].copy_(value)
        self.load_state_dict(own)
