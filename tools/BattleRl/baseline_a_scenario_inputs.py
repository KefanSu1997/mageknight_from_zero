"""A numerical/entity architecture with explicit complete-game additions."""
import hashlib,json
import gymnasium as gym
import numpy as np
from public_entity_tokens import PublicEntityTokens,original_vocabulary,KINDS
from baseline_a_world_inputs import encode_world
from baseline_a_world_entities import WorldEntityCodec,WORLD_ROLES
from baseline_a_visible_inventory import VisibleInventoryCodec,VISIBLE_ROLES
from baseline_a_combat_entities import CombatEntityCodec,COMBAT_ROLES
from baseline_a_unit_resources import UnitResourceCodec,RESOURCE_ROLES
from baseline_a_pending_entities import PendingEntityCodec,PENDING_ROLES
from baseline_a_scenario_actions import prepare_support


class ScenarioInputs:
    def __init__(self,repository,schema,text_assets,entity_ids,capacity=512):
        self.schema=schema;self.text_assets=text_assets;self.capacity=capacity
        self.layout=PublicEntityTokens(original_vocabulary(repository))
        if self.layout.ids!=list(entity_ids):raise ValueError('Original A entity vocabulary mismatch')
        self.world=WorldEntityCodec(repository,entity_ids)
        offset=len(KINDS)+len(WORLD_ROLES)
        self.visible=VisibleInventoryCodec(entity_ids,offset);offset+=len(VISIBLE_ROLES)
        self.combat=CombatEntityCodec(repository,entity_ids,offset);offset+=len(COMBAT_ROLES)
        self.resources=UnitResourceCodec(repository,entity_ids,offset);offset+=len(RESOURCE_ROLES)
        self.pending=PendingEntityCodec(entity_ids,offset);self.roles=offset+len(PENDING_ROLES)
        self.space=gym.spaces.Dict(dict(
            state=gym.spaces.Box(-np.inf,np.inf,(6471,),np.float32),
            entity_ids=gym.spaces.Box(0,len(entity_ids),(capacity,),np.int16),
            entity_roles=gym.spaces.Box(0,self.roles-1,(capacity,),np.uint8),
            entity_attributes=gym.spaces.Box(-np.inf,np.inf,(capacity,54),np.float32),
            entity_present=gym.spaces.Box(0,1,(capacity,),np.uint8),
            context_id=gym.spaces.Box(0,2**24-1,(1,),np.float32)))

    def encode(self,native,history=None):
        observation=native['observation'];self.schema.validate(observation)
        features=np.asarray(native['baseline_a_features'],np.float32)
        if features.shape!=(6314,) or not np.isfinite(features).all():raise ValueError('Invalid A observation')
        if native['baseline_a_counter_schema']!='solo_decisions_completedTurnsPlus1_round_noLegacyPlanningWindow_v1':
            raise ValueError('Unknown A counter mapping')
        history=np.zeros(97,np.float32) if history is None else np.asarray(history,np.float32)
        if history.shape!=(97,) or not np.isfinite(history).all():raise ValueError('Invalid previous action/reward history')
        commands=native['commands']
        if observation['Terminal']:
            if commands or native['support_sha256'] is not None:raise ValueError('Terminal carries support')
            support=None
        else:
            payload=native['support_payload_json']
            if hashlib.sha256(payload.encode()).hexdigest()!=native['support_sha256']:raise ValueError('Support digest mismatch')
            signed=json.loads(payload)
            if set(signed)!={'version','step','observation','commands'} or signed['version']!=1 or signed['step']!=observation['Step'] or json.loads(signed['observation'])!=observation or signed['commands']!=commands:
                raise ValueError('Observation/support mismatch')
            if len(set(commands))!=len(commands):raise ValueError('Duplicate complete commands')
            support=prepare_support(observation,commands,self.schema,self.text_assets)
        tokens,old=self.layout._one(features)
        entities=[(self.layout.id_index[i],KINDS.index(k),tokens[n,len(self.layout.ids)+len(KINDS):]) for n,(i,k,_) in enumerate(old)]
        world=encode_world(observation['World'])
        if len(world['numeric'])!=60:raise ValueError('World numeric schema changed')
        entities+=self.world.encode(world['records'])+self.visible.encode(observation)
        entities+=self.combat.encode(observation['Combat'])+self.resources.encode(observation['Inventory'])+self.pending.encode(observation['Offers'])
        if len(entities)>self.capacity:raise ValueError('Full public entities exceed declared capacity; never truncate')
        result=dict(state=np.concatenate((features,history,world['numeric'])).astype(np.float32),
            entity_ids=np.zeros(self.capacity,np.int16),entity_roles=np.zeros(self.capacity,np.uint8),
            entity_attributes=np.zeros((self.capacity,54),np.float32),entity_present=np.zeros(self.capacity,np.uint8),
            context_id=np.zeros(1,np.float32))
        for n,(identity,role,attributes) in enumerate(entities):
            result['entity_ids'][n]=identity;result['entity_roles'][n]=role
            result['entity_attributes'][n]=attributes;result['entity_present'][n]=1
        if not self.space.contains(result):raise ValueError('Scenario observation space mismatch')
        return result,support

    def bind(self,native,runtime,document,history=None):
        observation,support=self.encode(native,history)
        if support is not None:observation['context_id'][0]=runtime.add(support,document)
        return observation,support
