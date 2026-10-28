"""Public physical references for the deed branch of the solo A adapter.

This supplements, never replaces, the complete numerical command. Other action
families must receive their own explicit mappings before formal integration.
"""
from copy import deepcopy

SUPPORTED_OPERATIONS = frozenset(('Spatial','Clock','TacticInput','TacticAbility','EndTurn','Rest',
    'LevelReward','Healing','Site','AssaultSettlement','Deed','Attack','Block','Damage','Basic','Unit',
    'CombatFollowup','Skill','Adventure','Artifact','MaterialArtifact','Banner','Shapeshift',
    'CityArtifactReward','Repeated','Learning'))


def world_entities(observation, kind, command):
    """Public coordinates and revealed defenders only; no unrevealed tile face."""
    result=[]
    world=observation['World']
    def cell(q,r,path):
        matches=[(i,c) for i,c in enumerate(world['Cells']) if c['Q']==q and c['R']==r]
        if len(matches)!=1:
            raise ValueError('Target is not one unique revealed map cell')
        index, target=matches[0]
        result.append(dict(parameter=path,zone=('World','Cells'),slot=index,entity=deepcopy(target)))
        for guard in (target.get('Site') or {}).get('Guards',[]):
            if not guard['Revealed'] and guard['DefinitionId'] is not None:
                raise ValueError('Hidden guard identity leaked into public input')
            result.append(dict(parameter=path+('Guard',guard['Slot']),
                               zone=('World','Cells',index,'Site','Guards'),slot=guard['Slot'],
                               entity=deepcopy(guard)))
    if kind=='Spatial':
        operation=command['Kind']
        if type(operation) is not int or operation not in range(5):
            raise ValueError('Unknown spatial operation')
        if operation==1:
            if not any(p['Q']==command['Q'] and p['R']==command['R'] for p in world['ExplorationPositions']):
                raise ValueError('Unbound public exploration position')
            # The target is an unrevealed tile anchor, not an existing cell.
            cell(world['PlayerQ'],world['PlayerR'],('ExplorationOrigin',))
        else:
            cell(command['Q'],command['R'],('TargetCell',))
        if operation==4:
            result.append(_physical_reference(observation,('SkillSlot',),('Inventory','Skills'),command['SkillSlot']))
    elif kind in ('Adventure','Site','AssaultSettlement'):
        cell(world['PlayerQ'],world['PlayerR'],('CurrentCell',))
    elif kind=='CombatFollowup':
        operation=command['Kind']
        if type(operation) is not int or operation not in range(5):
            raise ValueError('Unknown combat followup')
        enemy=command['EnemySlot']
        if enemy!=-1:
            result.append(_physical_reference(observation,('EnemySlot',),('Combat','Enemies'),enemy))
        elif operation==0:
            raise ValueError('Attack reduction requires an enemy')
        # The full enemy includes all attack segments; AttackIndex remains in
        # the complete command and is never treated as a global enemy slot.
    elif kind!='Clock':
        raise ValueError('Unsupported world operation')
    return result


def candidate_entities(observation, envelope):
    """Dispatch one complete native command; unsupported/new branches fail closed.

    This checks version/staleness and references, not native legality. Callers
    must supply a member of the archived legal support, never arbitrary commands.
    """
    if (type(envelope.get('SchemaVersion')) is not int or envelope['SchemaVersion']!=1
        or type(envelope.get('ExpectedStep')) is not int or envelope['ExpectedStep']!=observation['Step']):
        raise ValueError('Stale or unsupported native candidate')
    payload=[(k,v) for k,v in envelope.items() if k not in ('SchemaVersion','ExpectedStep') and v is not None]
    if len(payload)!=1:
        raise ValueError('Exactly one complete operation required')
    kind, command=payload[0]
    if kind not in SUPPORTED_OPERATIONS:
        raise ValueError('Unmapped native operation: '+kind)
    if kind=='Deed':return deed_entities(observation,command)
    if kind=='Unit':return unit_entities(observation,command)
    if kind=='Skill':return skill_entities(observation,command)
    if kind in ('Attack','Block','Damage'):return combat_entities(observation,kind,command)
    if kind in ('Basic','Rest','Healing','Learning'):return lifecycle_entities(observation,kind,command)
    if kind in ('EndTurn','LevelReward','CityArtifactReward'):return reward_entities(observation,kind,command)
    if kind in ('Artifact','Banner','Shapeshift'):return equipment_entities(observation,kind,command)
    if kind in ('MaterialArtifact','Repeated'):return copied_effect_entities(observation,kind,command)
    if kind in ('Site','TacticInput','TacticAbility'):
        refs=interaction_entities(observation,kind,command)
        return refs+world_entities(observation,kind,command) if kind=='Site' else refs
    if kind in ('Spatial','Clock','Adventure','AssaultSettlement','CombatFollowup'):
        return world_entities(observation,kind,command)
    raise ValueError('Unmapped native operation: '+kind)


def equipment_entities(observation, kind, command):
    """Artifact, attached-banner and shapeshift physical identities.

    Mode, mana colors, output kind, terrain and coordinates remain in the full
    structured command; this function adds original entities, not replacement IDs.
    """
    result = []
    def bind(path, zone, slot, optional=False, identity='Slot'):
        if optional and type(slot) is int and slot == -1:
            return
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or not 0 <= slot < len(items) or items[slot][identity] != slot:
            raise ValueError(f'Unbound equipment parameter {path}')
        result.append(dict(parameter=path,zone=zone,slot=slot,entity=deepcopy(items[slot])))
    if kind == 'Artifact':
        bind(('HandSlot',),('Inventory','Hand'),command['HandSlot'])
        for field, zone in [('UnitSlot',('Inventory','Units')),('OfferedUnitSlot',('Offers','Units')),
                            ('DisbandUnitSlot',('Inventory','Units'))]:
            bind((field,),zone,command.get(field,-1),optional=True)
        for field, zone in [('MaterialSlots',('Inventory','Hand')),('EnemySlots',('Combat','Enemies'))]:
            for i, slot in enumerate(command.get(field) or []):
                bind((field,i),zone,slot)
    elif kind == 'Banner':
        bind(('UnitSlot',),('Inventory','Units'),command['UnitSlot'])
        mode=command['Kind']
        if type(mode) is not int or mode not in (0,1,2):
            raise ValueError('Unknown banner operation')
        if mode == 1:
            enemy=command['EnemySlot']
            bind(('EnemySlot',),('Combat','Enemies'),enemy)
            bind(('AttackIndex',),('Combat','Enemies',enemy,'Attacks'),command['AttackIndex'],identity='Index')
        elif mode == 2:
            bind(('PlayedSlot',),('Inventory','Played'),command['PlayedSlot'])
    elif kind == 'Shapeshift':
        bind(('SkillSlot',),('Inventory','Skills'),command['SkillSlot'])
        bind(('HandSlot',),('Inventory','Hand'),command['HandSlot'])
        bind(('MaterialSlot',),('Inventory','Hand'),command.get('MaterialSlot',-1),optional=True)
    else:
        raise ValueError('Unsupported equipment operation')
    return result


def interaction_entities(observation, kind, command):
    """Resolve site and tactic choices without revealing blind supply order.

    SearchDeck CardId is disclosed by the native legal command itself. It has
    no public draw-pile slot; do not invent one or inspect a private checkpoint.
    """
    result = []
    def bind(path, zone, slot):
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or not 0 <= slot < len(items) or items[slot]['Slot'] != slot:
            raise ValueError(f'Unbound interaction parameter {path}')
        result.append(dict(parameter=path, zone=zone, slot=slot, entity=deepcopy(items[slot])))
    if kind == 'Site':
        operation = command['Operation']
        if type(operation) is not int or operation not in range(11):
            raise ValueError('Unknown site operation')
        zones = {0:('Offers','Units'), 1:('Offers','Spells'), 3:('Inventory','Units'),
                 5:('Offers','AdvancedActions'), 9:('Offers','MonasteryActions')}
        if operation in zones:
            bind(('Slot',), zones[operation], command['Slot'])
        elif command['Slot'] != -1:
            raise ValueError('Blind or slotless purchase must not expose a target')
        banner = command['CommandBannerPlayedSlot']
        if type(banner) is not int or banner < -1:
            raise ValueError('Invalid command banner slot')
        if banner != -1:
            if operation != 0:
                raise ValueError('Command banner requires recruitment')
            bind(('CommandBannerPlayedSlot',), ('Inventory','Played'), banner)
    elif kind == 'TacticInput':
        operation = command['Kind']
        if type(operation) is not int or operation not in (0,1,2):
            raise ValueError('Unknown tactic input')
        if operation == 0:
            for i, slot in enumerate(command['HandSlots']):
                bind(('HandSlots',i), ('Inventory','Hand'), slot)
        elif operation == 1:
            bind(('DieSlot',), ('Inventory','Source','Available'), command['DieSlot'])
        else:
            identity = command['CardId']
            if not isinstance(identity,str) or not identity:
                raise ValueError('Native searchable card identity required')
            result.append(dict(parameter=('CardId',),zone=('LegalCommand','CardId'),slot=None,
                               entity=dict(Id=identity)))
    elif kind == 'TacticAbility':
        operation = command['Kind']
        if type(operation) is not int or operation not in range(7):
            raise ValueError('Unknown tactic ability')
        if operation in (0,6):
            zone = ('Inventory','Hand') if operation == 0 else ('Inventory','Source','Available')
            for i, slot in enumerate(command['Slots']):
                bind(('Slots',i),zone,slot)
        elif command['Slots']:
            raise ValueError('Slotless tactic has unexpected parameters')
    else:
        raise ValueError('Unsupported interaction operation')
    return result


def reward_entities(observation, kind, command):
    """Bind visible reward offers and pre-cleanup physical choices.

    A committed end-turn has already discarded/healed the selected cards. Its
    echoed slot numbers are locked receipt fields, not indices in the new hand.
    """
    result = []
    def bind(path, zone, slot, optional=False):
        if optional and type(slot) is int and slot == -1:
            return
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or not 0 <= slot < len(items) or items[slot]['Slot'] != slot:
            raise ValueError(f'Unbound reward parameter {path}')
        result.append(dict(parameter=path, zone=zone, slot=slot, entity=deepcopy(items[slot])))

    if kind == 'LevelReward':
        offer = command['Offer']
        if type(offer) is not int or offer not in (0,1):
            raise ValueError('Unknown skill reward source')
        bind(('SkillSlot',), ('Offers', 'LevelSkills' if offer == 0 else 'CommonSkills'), command['SkillSlot'])
        bind(('ActionSlot',), ('Offers','AdvancedActions'), command['ActionSlot'])
    elif kind == 'CityArtifactReward':
        for i, slot in enumerate(command['KeepSlots']):
            bind(('KeepSlots',i), ('Offers','CityArtifacts'), slot)
    elif kind == 'EndTurn':
        locked = observation['Offers'].get('CommittedRewardEnd')
        if locked is not None:
            for field in ('DiscardHandSlots','WoundZone','WoundSlot','MineColor'):
                if command[field] != locked[field]:
                    raise ValueError('Cannot revise committed cleanup choices')
        else:
            for i, slot in enumerate(command['DiscardHandSlots']):
                bind(('DiscardHandSlots',i), ('Inventory','Hand'), slot)
            zone = command['WoundZone']
            if type(zone) is not int or zone not in (0,1,2):
                raise ValueError('Unknown wound cleanup zone')
            if zone:
                bind(('WoundSlot',), ('Inventory','Hand' if zone == 1 else 'Discard'), command['WoundSlot'])
                if result[-1]['entity']['Type'] != 'Wound':
                    raise ValueError('Cleanup wound reference is not a wound')
            elif command['WoundSlot'] != -1:
                raise ValueError('Absent cleanup wound must not have a slot')
        for field, zone in [('SpellRewardSlot','Spells'),('ArtifactRewardSlot','AdventureArtifacts')]:
            bind((field,), ('Offers',zone), command[field], optional=True)
        ruin = command.get('RuinReward')
        if ruin is not None:
            for i, slot in enumerate(ruin['ArtifactSlots']):
                bind(('RuinReward','ArtifactSlots',i), ('Offers','RuinArtifacts'), slot)
            for field, zone in [('SpellSlot','Spells'),('AdvancedSlot','AdvancedActions'),('UnitSlot','Units')]:
                bind(('RuinReward',field), ('Offers',zone), ruin[field], optional=True)
            bind(('RuinReward','DisbandSlot'), ('Inventory','Units'), ruin['DisbandSlot'], optional=True)
    else:
        raise ValueError('Unsupported reward operation')
    return result


def lifecycle_entities(observation, kind, command):
    """Physical references for native basic/rest/healing/learning operations.

    Healing has no hand-slot choice: expose the hand-wound pool, without
    inventing a selectable wound index. The untouched command remains decisive.
    """
    result = []
    def bind(path, zone, slot):
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or not 0 <= slot < len(items) or items[slot]['Slot'] != slot:
            raise ValueError(f'Unbound lifecycle parameter {path}')
        result.append(dict(parameter=path, zone=zone, slot=slot, entity=deepcopy(items[slot])))
    if kind == 'Basic':
        operation = command['Kind']
        if type(operation) is not int:
            raise ValueError('Native SoloBasicKind integer required')
        if operation == 0:
            bind(('Slot',), ('Inventory', 'Source', 'Available'), command['Slot'])
        elif operation in (1, 2, 3):
            bind(('Slot',), ('Inventory', 'Hand'), command['Slot'])
        elif operation != 4 or command['Slot'] != -1:
            raise ValueError('Unknown basic operation or unexpected phase-advance slot')
    elif kind == 'Rest':
        ordinary = command['OrdinaryHandSlot']
        if type(ordinary) is not int or ordinary < -1:
            raise ValueError('Invalid ordinary rest slot')
        if ordinary != -1:
            bind(('OrdinaryHandSlot',), ('Inventory', 'Hand'), ordinary)
            if result[-1]['entity']['Type'] == 'Wound':
                raise ValueError('Rest ordinary card cannot be a wound')
        wounds = command['WoundHandSlots']
        if len(set(wounds)) != len(wounds):
            raise ValueError('Duplicate rest wound')
        for i, slot in enumerate(wounds):
            bind(('WoundHandSlots', i), ('Inventory', 'Hand'), slot)
            if result[-1]['entity']['Type'] != 'Wound':
                raise ValueError('Rest wound selection is not a wound')
    elif kind == 'Healing':
        slot = command['UnitSlot']
        if type(slot) is not int or slot < -1:
            raise ValueError('Invalid healing target')
        if slot != -1:
            bind(('UnitSlot',), ('Inventory', 'Units'), slot)
        else:
            for card in observation['Inventory']['Hand']:
                if card['Type'] == 'Wound':
                    bind(('HandWoundPool', card['Slot']), ('Inventory', 'Hand'), card['Slot'])
            if not result:
                raise ValueError('No actual hand wound to heal')
    elif kind == 'Learning':
        bind(('OfferSlot',), ('Offers', 'AdvancedActions'), command['OfferSlot'])
    else:
        raise ValueError('Unsupported lifecycle operation')
    return result


def skill_entities(observation, command):
    """Match SoloSkillInputs physical zones, including mode-dependent puppets."""
    result = []

    def bind(path, zone, slot, identity='Slot', optional=False):
        if optional and slot == -1:
            return None
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or slot < 0 or slot >= len(items) or items[slot][identity] != slot:
            raise ValueError(f'Unbound skill parameter {path}')
        entity = deepcopy(items[slot])
        result.append(dict(parameter=path, zone=zone, slot=slot, entity=entity))
        return entity

    skill = bind(('SkillSlot',), ('Inventory', 'Skills'), command['SkillSlot'])
    for field, zone in {
        'UnitSlot': ('Inventory', 'Units'), 'WoundSlot': ('Inventory', 'Hand'),
        'HandSlot': ('Inventory', 'Hand'), 'AttackSourceSlot': ('Combat', 'Attacks'),
        'EnemySlot': ('Combat', 'Enemies'),
    }.items():
        bind((field,), zone, command.get(field, -1), optional=True)
    for i, slot in enumerate(command.get('EnemySlots') or []):
        bind(('EnemySlots', i), ('Combat', 'Enemies'), slot)
    option = command['Option']
    if command.get('PuppetSlot', -1) != -1:
        if option not in (0, 1, 2):
            raise ValueError('Unknown puppet mode')
        zone = 'PuppetCandidates' if option == 0 else 'PuppetStored'
        bind(('PuppetSlot',), ('Inventory', zone), command['PuppetSlot'])
    # Native BattleSkillChoice.NeedsAttack: only these choices select an attack
    # segment. An unrelated default AttackIndex=0 must not imply a target.
    key = skill['Id']
    needs_attack = (key in ('hero_skill_pool_004:07', 'hero_skill_pool_003:01', 'hero_skill_pool_004:06')
        or key == 'hero_skill_pool_001:01' and option == 1
        or key == 'hero_skill_pool_003:08' and option == 0
        or key == 'hero_skill_pool_004:05' and option >= 200)
    if needs_attack and command.get('EnemySlot', -1) >= 0:
        bind(('AttackIndex',), ('Combat', 'Enemies', command['EnemySlot'], 'Attacks'),
             command['AttackIndex'], 'Index')
    return result


def unit_entities(observation, command):
    """Use the actual printed ability to disambiguate enemy versus ready-unit targets."""
    result = []

    def bind(field, zone, identity='Slot', optional=False):
        slot = command.get(field, -1)
        if optional and slot == -1:
            return None
        items = observation
        for key in zone:
            items = items[key]
        if type(slot) is not int or slot < 0 or slot >= len(items) or items[slot][identity] != slot:
            raise ValueError(f'Unbound {field}')
        entity = deepcopy(items[slot])
        result.append(dict(parameter=(field,), zone=zone, slot=slot, entity=entity))
        return entity

    bind('UnitSlot', ('Inventory', 'Units'))
    ability = bind('Ability', ('Inventory', 'Units', command['UnitSlot'], 'Actions'), 'Index')
    bind('LeadershipSlot', ('Inventory', 'Skills'), optional=True)
    kind = ability['Kind']
    definition = ability['Definition']
    enemy = kind in ('FreezeArmor', 'StopUnfortifiedEnemy', 'StopEnemy', 'DestroyBlockedEnemy',
                     'ReduceEnemyArmor', 'RemoveEnemyDefenses', 'WeakenEnemyAttack', 'InterceptEnemyAttack')
    enemy = enemy or definition['RemoveTargetFortifications'] or definition['RemoveTargetResistances']
    if kind == 'ReadyUnit':
        bind('TargetSlot', ('Inventory', 'Units'))
    elif enemy:
        bind('TargetSlot', ('Combat', 'Enemies'))
        if kind in ('WeakenEnemyAttack', 'InterceptEnemyAttack'):
            bind('AttackIndex', ('Combat', 'Enemies', command['TargetSlot'], 'Attacks'), 'Index')
    elif command.get('TargetSlot', -1) != -1:
        raise ValueError('Unrelated target on targetless printed ability')
    return result


def combat_entities(observation, kind, command):
    """Resolve complete attack/block/damage references, preserving allocation order.

    Enemy defend sources are enemies, not player units or attack contributions.
    AttackIndex is local to its selected enemy, never a global enemy index.
    """
    bindings = []

    def bind(path, zone, slot, identity='Slot'):
        if type(slot) is not int or slot < 0:
            raise ValueError(f'Invalid physical slot at {path}')
        items = observation
        for key in zone:
            items = items[key]
        if slot >= len(items) or items[slot][identity] != slot:
            raise ValueError(f'Unbound public slot at {path}')
        bindings.append(dict(parameter=path, zone=zone, slot=slot,
                             entity=deepcopy(items[slot])))

    def attack(path, value):
        enemy = value['EnemySlot']
        bind(path+('EnemySlot',), ('Combat', 'Enemies'), enemy)
        bind(path+('AttackIndex',), ('Combat', 'Enemies', enemy, 'Attacks'),
             value['AttackIndex'], 'Index')

    if kind == 'Attack':
        for i, slot in enumerate(command['Targets']):
            bind(('Targets', i), ('Combat', 'Enemies'), slot)
        for i, slot in enumerate(command['ContributionSlots']):
            bind(('ContributionSlots', i), ('Combat', 'Attacks'), slot)
        for i, pair in enumerate(command['Defense']):
            for field in ('Source', 'Target'):
                bind(('Defense', i, field), ('Combat', 'Enemies'), pair[field])
    elif kind == 'Block':
        attack((), command)
        for i, slot in enumerate(command['ContributionSlots']):
            bind(('ContributionSlots', i), ('Combat', 'Blocks'), slot)
    elif kind == 'Damage':
        for i, allocation in enumerate(command['Allocations']):
            path = ('Allocations', i)
            attack(path, allocation)
            for j, slot in enumerate(allocation['UnitSlots']):
                bind(path+('UnitSlots', j), ('Inventory', 'Units'), slot)
    else:
        raise ValueError('Unsupported combat operation; explicit mapping required')
    return bindings


def selection_entities(observation, inputs, prefix=('Inputs',)):
    bindings = []

    def bind(path, zone, slot, optional=False):
        if optional and slot == -1:
            return
        if type(slot) is not int or slot < 0:
            raise ValueError(f'Invalid physical slot at {path}')
        items = observation
        for key in zone:
            items = items[key]
        if slot >= len(items) or items[slot]['Slot'] != slot:
            raise ValueError(f'Unbound public slot at {path}')
        bindings.append(dict(parameter=path, zone=zone, slot=slot,
                             entity=deepcopy(items[slot])))

    def selection(value, path):
        if value is None:
            return
        arrays = {
            'HandSlots': ('Inventory', 'Hand'),
            'DiscardSlots': ('Inventory', 'Discard'),
            'DiceSlots': ('Inventory', 'Source', 'Available'),
            'SpellSlots': ('Offers', 'Spells'),
            'UnitSlots': ('Inventory', 'Units'),
            'EnemyIndices': ('Combat', 'Enemies'),
        }
        singles = {
            'AdvancedSlot': ('Offers', 'AdvancedActions'),
            'UnitSlot': ('Inventory', 'Units'),
            'OfferedUnitSlot': ('Offers', 'Units'),
            'DisbandUnitSlot': ('Inventory', 'Units'),
            'TargetIndex': ('Combat', 'Enemies'),
        }
        if value.get('HandSlots') and value.get('DiscardSlots'):
            raise ValueError('Mixed material zones rejected by native deed resolver')
        for field, zone in arrays.items():
            for i, slot in enumerate(value.get(field) or []):
                bind(path+(field, i), zone, slot)
        for field, zone in singles.items():
            bind(path+(field,), zone, value.get(field, -1), optional=True)
        selection(value.get('BorrowedInputs'), path+('BorrowedInputs',))

    selection(inputs, prefix)
    return bindings


def _physical_reference(observation, path, zone, slot):
    items=observation
    for key in zone:
        items=items[key]
    if type(slot) is not int or not 0 <= slot < len(items) or items[slot]['Slot'] != slot:
        raise ValueError(f'Unbound physical parameter {path}')
    return dict(parameter=path,zone=zone,slot=slot,entity=deepcopy(items[slot]))


def deed_entities(observation, deed):
    result=[_physical_reference(observation,('HandSlot',),('Inventory','Hand'),deed['HandSlot'])]
    if deed.get('EnemySlot',-1) != -1:
        result.append(_physical_reference(observation,('EnemySlot',),('Combat','Enemies'),deed['EnemySlot']))
    result.extend(selection_entities(observation,deed.get('Inputs')))
    return result


def copied_effect_entities(observation, kind, command):
    """Resolve material artifacts and committed repeated effects against public state."""
    result=[]
    def bind(field, zone, optional=False):
        slot=command.get(field,-1)
        if optional and type(slot) is int and slot == -1:
            return
        ref=_physical_reference(observation,(field,),zone,slot)
        result.append(ref)
        return ref['entity']
    if kind == 'Repeated':
        pending=observation['Offers']['RepeatedEffect']
        if pending is None or not pending['CardId']:
            raise ValueError('Missing committed repeated effect identity')
        result.append(dict(parameter=('RepeatedEffect',),zone=('Offers','RepeatedEffect'),slot=None,
                           entity=dict(Id=pending['CardId'])))
        result.extend(selection_entities(observation,command.get('Inputs')))
    elif kind == 'MaterialArtifact':
        card=bind('HandSlot',('Inventory','Hand'))
        identity=card['Id']
        if identity not in ('items_013','items_021','items_023'):
            raise ValueError('Unsupported material artifact')
        bind('MaterialSlot',('Inventory','Hand'),optional=True)
        if identity in ('items_013','items_023'):
            zone='Spells' if identity == 'items_023' or command['Burn'] else 'AdvancedActions'
            bind('OfferSlot',('Offers',zone))
        else:
            bind('UnitSlot',('Inventory','Units'),optional=True)
        if identity == 'items_023':
            bind('EnemySlot',('Combat','Enemies'),optional=True)
            result.extend(selection_entities(observation,command.get('SpellInputs'),('SpellInputs',)))
    else:
        raise ValueError('Unsupported copied effect operation')
    return result
