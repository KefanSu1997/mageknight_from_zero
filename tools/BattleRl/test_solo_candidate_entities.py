import unittest
from solo_candidate_entities import deed_entities, combat_entities, unit_entities, skill_entities, lifecycle_entities, reward_entities, interaction_entities
from solo_candidate_entities import equipment_entities
from solo_candidate_entities import copied_effect_entities, selection_entities
from solo_candidate_entities import candidate_entities, world_entities


class CandidateEntityTests(unittest.TestCase):
    def test_public_world_targets_and_dispatch(self):
        obs=dict(Step=4,World=dict(PlayerQ=0,PlayerR=0,
            Cells=[dict(Q=0,R=0,Terrain='Plains',Site=None),
                   dict(Q=1,R=0,Terrain='Hills',Site=dict(Guards=[dict(Slot=0,Revealed=False,DefinitionId=None)]))],
            ExplorationPositions=[dict(Q=2,R=1)]))
        refs=candidate_entities(obs,dict(SchemaVersion=1,ExpectedStep=4,Spatial=dict(Kind=0,Q=1,R=0)))
        self.assertEqual(refs[0]['entity']['Terrain'],'Hills')
        self.assertIsNone(refs[1]['entity']['DefinitionId'])
        refs=world_entities(obs,'Spatial',dict(Kind=1,Q=2,R=1))
        self.assertEqual(refs[0]['parameter'],('ExplorationOrigin',))
        self.assertEqual(refs[0]['entity']['Q'],0)
        for envelope in [dict(SchemaVersion=True,ExpectedStep=4,Clock={}),
                         dict(SchemaVersion=1,ExpectedStep=3,Clock={}),
                         dict(SchemaVersion=1,ExpectedStep=4,Clock={},Rest={}),
                         dict(SchemaVersion=1,ExpectedStep=4,NewAction={})]:
            with self.assertRaises(ValueError):candidate_entities(obs,envelope)
        obs['World']['Cells'][1]['Site']['Guards'][0]['DefinitionId']='hidden'
        with self.assertRaises(ValueError):world_entities(obs,'Spatial',dict(Kind=0,Q=1,R=0))

    def test_tome_and_repeated_nested_targets(self):
        obs=dict(Inventory=dict(Hand=[dict(Slot=0,Id='items_023'),dict(Slot=1,Id='material')]),
                 Offers=dict(Spells=[dict(Slot=0,Id='magic_001')],RepeatedEffect=dict(CardId='basic_card_023')),
                 Combat=dict(Enemies=[dict(Slot=0,Id='monster_000')]))
        selection=dict(HandSlots=[1],TargetIndex=0,EnemyIndices=[0],BorrowedInputs=dict(TargetIndex=0))
        refs=copied_effect_entities(obs,'MaterialArtifact',dict(HandSlot=0,MaterialSlot=1,OfferSlot=0,
                                                               EnemySlot=0,SpellInputs=selection))
        self.assertEqual([r['entity']['Id'] for r in refs[:4]],['items_023','material','magic_001','monster_000'])
        self.assertEqual(refs[-1]['parameter'],('SpellInputs','BorrowedInputs','TargetIndex'))
        refs=copied_effect_entities(obs,'Repeated',dict(Inputs=selection))
        self.assertEqual(refs[0]['entity']['Id'],'basic_card_023')
        self.assertIsNone(refs[0]['slot'])
        self.assertEqual(refs[-1]['parameter'],('Inputs','BorrowedInputs','TargetIndex'))
        with self.assertRaises(ValueError):selection_entities(obs,dict(TargetIndex=1))

    def test_equipment_material_and_target_zones(self):
        obs=dict(Inventory=dict(Hand=[dict(Slot=0,Id='artifact'),dict(Slot=1,Id='material')],
                                 Units=[dict(Slot=0,Id='owned',Banner='items_001')],
                                 Played=[dict(Slot=0,Id='playedbanner')],Skills=[dict(Slot=0,Id='shapeshift')]),
                 Offers=dict(Units=[dict(Slot=0,Id='offered')]),
                 Combat=dict(Enemies=[dict(Slot=0,Id='enemy',Attacks=[dict(Index=0,Power=5)])]))
        refs=equipment_entities(obs,'Artifact',dict(HandSlot=0,MaterialSlots=[1],EnemySlots=[0],OfferedUnitSlot=0,DisbandUnitSlot=0))
        self.assertEqual([r['entity']['Id'] for r in refs],['artifact','offered','owned','material','enemy'])
        refs=equipment_entities(obs,'Banner',dict(Kind=1,UnitSlot=0,EnemySlot=0,AttackIndex=0))
        self.assertEqual(refs[-1]['zone'],('Combat','Enemies',0,'Attacks'))
        refs=equipment_entities(obs,'Banner',dict(Kind=2,UnitSlot=0,PlayedSlot=0))
        self.assertEqual(refs[-1]['entity']['Id'],'playedbanner')
        refs=equipment_entities(obs,'Shapeshift',dict(SkillSlot=0,HandSlot=0,MaterialSlot=1))
        self.assertEqual([r['entity']['Id'] for r in refs],['shapeshift','artifact','material'])
        with self.assertRaises(ValueError):
            equipment_entities(obs,'Banner',dict(Kind=1,UnitSlot=0,EnemySlot=0,AttackIndex=1))

    def test_interactions_keep_hidden_draw_positions_hidden(self):
        obs=dict(Inventory=dict(Hand=[dict(Slot=0,Id='hand')],Played=[dict(Slot=0,Id='banner')],
                                Units=[dict(Slot=0,Id='owned')],Source=dict(Available=[dict(Slot=0,Face='Red')])),
                 Offers=dict(Units=[dict(Slot=0,Id='recruit')],Spells=[dict(Slot=0,Id='spell')],
                             AdvancedActions=[dict(Slot=0,Id='action')],MonasteryActions=[dict(Slot=0,Id='monastery')]))
        for op, expected in [(0,'recruit'),(1,'spell'),(3,'owned'),(5,'action'),(9,'monastery')]:
            refs=interaction_entities(obs,'Site',dict(Operation=op,Slot=0,CommandBannerPlayedSlot=-1))
            self.assertEqual(refs[0]['entity']['Id'],expected)
        refs=interaction_entities(obs,'Site',dict(Operation=0,Slot=0,CommandBannerPlayedSlot=0))
        self.assertEqual(refs[1]['entity']['Id'],'banner')
        self.assertEqual(interaction_entities(obs,'Site',dict(Operation=6,Slot=-1,CommandBannerPlayedSlot=-1)),[])
        with self.assertRaises(ValueError):
            interaction_entities(obs,'Site',dict(Operation=6,Slot=0,CommandBannerPlayedSlot=-1))
        refs=interaction_entities(obs,'TacticInput',dict(Kind=2,CardId='basic_card_000'))
        self.assertIsNone(refs[0]['slot'])
        self.assertEqual(refs[0]['zone'],('LegalCommand','CardId'))
        for kind, cmd, zone in [('TacticInput',dict(Kind=0,HandSlots=[0]),('Inventory','Hand')),
                               ('TacticInput',dict(Kind=1,DieSlot=0),('Inventory','Source','Available')),
                               ('TacticAbility',dict(Kind=6,Slots=[0]),('Inventory','Source','Available'))]:
            self.assertEqual(interaction_entities(obs,kind,cmd)[0]['zone'],zone)

    def test_reward_offers_and_committed_cleanup(self):
        obs = dict(Inventory=dict(Hand=[dict(Slot=0,Id='a',Type='Action')],
                                  Discard=[dict(Slot=0,Id='w',Type='Wound')], Units=[]),
                   Offers=dict(CommittedRewardEnd=None, Spells=[dict(Slot=0,Id='spell')],
                               AdventureArtifacts=[dict(Slot=0,Id='artifact')],
                               CityArtifacts=[dict(Slot=0,Id='cityartifact')],
                               LevelSkills=[dict(Slot=0,Id='ownskill')],
                               CommonSkills=[dict(Slot=0,Id='commonskill')],
                               AdvancedActions=[dict(Slot=0,Id='advanced')]))
        end=dict(DiscardHandSlots=[0],WoundZone=2,WoundSlot=0,MineColor=None,
                 SpellRewardSlot=0,ArtifactRewardSlot=0,RuinReward=None)
        refs=reward_entities(obs,'EndTurn',end)
        self.assertEqual([x['entity']['Id'] for x in refs],['a','w','spell','artifact'])
        # These indices still occur in the receipt after cleanup, but the hand
        # and discard no longer contain the originally selected physical cards.
        obs['Offers']['CommittedRewardEnd']=dict(end)
        obs['Inventory']['Hand']=[];obs['Inventory']['Discard']=[]
        refs=reward_entities(obs,'EndTurn',end)
        self.assertEqual([x['entity']['Id'] for x in refs],['spell','artifact'])
        with self.assertRaises(ValueError):
            reward_entities(obs,'EndTurn',dict(end,DiscardHandSlots=[]))
        for offer, identity in [(0,'ownskill'),(1,'commonskill')]:
            refs=reward_entities(obs,'LevelReward',dict(Offer=offer,SkillSlot=0,ActionSlot=0))
            self.assertEqual([x['entity']['Id'] for x in refs],[identity,'advanced'])
        refs=reward_entities(obs,'CityArtifactReward',dict(KeepSlots=[0]))
        self.assertEqual(refs[0]['entity']['Id'],'cityartifact')

    def test_lifecycle_zones_and_healing_pool(self):
        obs = dict(Inventory=dict(Hand=[dict(Slot=0, Id='card', Type='Action'),
                                       dict(Slot=1, Id='wound', Type='Wound')],
                                  Source=dict(Available=[dict(Slot=0, Face='Blue')]),
                                  Units=[dict(Slot=0, Id='unit')]),
                   Offers=dict(AdvancedActions=[dict(Slot=0, Id='offered')]))
        for kind, cmd, expected in [
            ('Basic', dict(Kind=0, Slot=0), ('Inventory','Source','Available')),
            ('Basic', dict(Kind=1, Slot=0), ('Inventory','Hand')),
            ('Learning', dict(OfferSlot=0), ('Offers','AdvancedActions')),
            ('Healing', dict(UnitSlot=0), ('Inventory','Units')),
            ('Healing', dict(UnitSlot=-1), ('Inventory','Hand'))]:
            refs = lifecycle_entities(obs, kind, cmd)
            self.assertEqual(refs[0]['zone'], expected)
        rest = lifecycle_entities(obs, 'Rest', dict(OrdinaryHandSlot=0, WoundHandSlots=[1]))
        self.assertEqual([x['slot'] for x in rest], [0,1])
        self.assertEqual(lifecycle_entities(obs, 'Basic', dict(Kind=4, Slot=-1)), [])
        for kind, cmd in [('Basic',dict(Kind=True,Slot=0)),
                          ('Rest',dict(OrdinaryHandSlot=1,WoundHandSlots=[])),
                          ('Rest',dict(OrdinaryHandSlot=0,WoundHandSlots=[1,1])),
                          ('Healing',dict(UnitSlot=-2))]:
            with self.assertRaises(ValueError):lifecycle_entities(obs,kind,cmd)

    def test_puppet_mode_uses_current_physical_zone(self):
        obs = self.combat_fixture()
        obs['Inventory'].update(Skills=[dict(Slot=0, Id='puppet')],
            PuppetCandidates=[dict(Slot=0, Id='newly_defeated')],
            PuppetStored=[dict(Slot=0, Id='previously_stored')])
        for mode, expected in ((0,'newly_defeated'),(1,'previously_stored'),(2,'previously_stored')):
            result = skill_entities(obs, dict(SkillSlot=0, Option=mode, PuppetSlot=0))
            self.assertEqual(result[-1]['entity']['Id'], expected)
        with self.assertRaises(ValueError):
            skill_entities(obs, dict(SkillSlot=0, Option=3, PuppetSlot=0))

    def test_skill_attack_reference_not_default_index(self):
        obs = self.combat_fixture()
        obs['Inventory']['Skills'] = [dict(Slot=0, Id='hero_skill_pool_001:01')]
        command = dict(SkillSlot=0, Option=1, EnemySlot=2, AttackIndex=1)
        result = skill_entities(obs, command)
        self.assertEqual(result[-1]['entity']['Power'], 7)
        command.update(Option=0, EnemySlot=-1, AttackIndex=0)
        self.assertEqual(len(skill_entities(obs, command)), 1)

    def test_unit_target_zone_follows_printed_ability(self):
        obs = self.combat_fixture()
        obs['Inventory']['Skills'] = []
        obs['Inventory']['Units'][0]['Actions'] = [dict(Index=0, Kind='ReadyUnit',
            Definition=dict(RemoveTargetFortifications=False, RemoveTargetResistances=False))]
        command = dict(UnitSlot=0, Ability=0, TargetSlot=1, AttackIndex=0)
        bound = unit_entities(obs, command)
        self.assertEqual(bound[-1]['entity']['Id'], 'unit1')
        obs['Inventory']['Units'][0]['Actions'][0]['Kind'] = 'WeakenEnemyAttack'
        bound = unit_entities(obs, command)
        self.assertEqual(bound[-2]['entity']['Id'], 'enemy1')
        self.assertEqual(bound[-1]['entity']['Power'], 3)
        obs['Inventory']['Units'][0]['Actions'][0]['Kind'] = 'Movement'
        with self.assertRaises(ValueError):unit_entities(obs, command)

    def combat_fixture(self):
        return dict(Combat=dict(Enemies=[dict(Slot=i, Id=f'enemy{i}',
            Attacks=[dict(Index=0, Power=i+2), dict(Index=1, Power=i+5)]) for i in range(3)],
            Attacks=[dict(Slot=0, Value=8, SourceCardId='attack_card')],
            Blocks=[dict(Slot=0, Value=4)]),
            Inventory=dict(Units=[dict(Slot=i, Id=f'unit{i}') for i in range(2)]))

    def test_attack_defenders_are_enemies(self):
        result = combat_entities(self.combat_fixture(), 'Attack', dict(Targets=[2],
            ContributionSlots=[0], Defense=[dict(Source=1, Target=2)]))
        self.assertEqual([r['zone'] for r in result], [('Combat', 'Enemies'),
            ('Combat', 'Attacks'), ('Combat', 'Enemies'), ('Combat', 'Enemies')])
        self.assertEqual(result[2]['entity']['Id'], 'enemy1')
        self.assertEqual(result[1]['entity']['SourceCardId'], 'attack_card')

    def test_attack_index_is_local_and_unit_order_preserved(self):
        result = combat_entities(self.combat_fixture(), 'Damage', dict(Allocations=[
            dict(EnemySlot=2, AttackIndex=1, UnitSlots=[1,0]),
            dict(EnemySlot=0, AttackIndex=0, UnitSlots=[])]))
        self.assertEqual(result[1]['entity']['Power'], 7)
        self.assertEqual([r['entity']['Id'] for r in result[2:4]], ['unit1','unit0'])
        self.assertEqual(len(result), 6)
        with self.assertRaises(ValueError):
            combat_entities(self.combat_fixture(), 'Block',
                dict(EnemySlot=0, AttackIndex=2, ContributionSlots=[0]))

    def fixture(self):
        cards = [dict(Slot=i, Id=f'card{i}') for i in range(300)]
        return dict(Inventory=dict(Hand=cards, Discard=[dict(Slot=0, Id='discard')],
                                   Units=[], Source=dict(Available=[])), Offers={})

    def test_physical_identity_and_order_without_fixed_cap(self):
        obs = self.fixture()
        result = deed_entities(obs, dict(HandSlot=299, Inputs=dict(HandSlots=[7, 2])))
        self.assertEqual([r['entity']['Id'] for r in result], ['card299', 'card7', 'card2'])
        self.assertEqual(result[1]['parameter'], ('Inputs', 'HandSlots', 0))
        obs['Inventory']['Hand'][7]['Id'] = 'changed'
        self.assertEqual(result[1]['entity']['Id'], 'card7')

    def test_nested_copy_material_uses_explicit_zone(self):
        result = deed_entities(self.fixture(), dict(HandSlot=0,
            Inputs=dict(BorrowedInputs=dict(DiscardSlots=[0]))))
        self.assertEqual(result[1]['entity']['Id'], 'discard')
        self.assertEqual(result[1]['parameter'], ('Inputs', 'BorrowedInputs', 'DiscardSlots', 0))

    def test_invalid_slot_never_falls_back_to_id_or_other_zone(self):
        for slot in (-1, 300, True):
            with self.assertRaises(ValueError):
                deed_entities(self.fixture(), dict(HandSlot=slot))
        obs = self.fixture(); obs['Inventory']['Hand'][0]['Slot'] = 1
        with self.assertRaises(ValueError):deed_entities(obs, dict(HandSlot=0))
        with self.assertRaises(ValueError):
            deed_entities(self.fixture(), dict(HandSlot=0, Inputs=dict(HandSlots=[1], DiscardSlots=[0])))


if __name__ == '__main__':unittest.main()
