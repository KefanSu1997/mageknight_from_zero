using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;

namespace MageKnight.Tests.RulesState
{
    public sealed class OriginalBrownEnemyRulesTests
    {
        private static string Root => Directory.GetCurrentDirectory();
        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(Path.Combine(Root,"resources/text_json")).Single(e=>e.Id==id);
        private static JArray Oracle => (JArray)JObject.Parse(File.ReadAllText(Path.Combine(Root,"AutomationConfigs/original_brown_combat_oracle_20261004.json")))["entries"];
        public static IEnumerable<string> BrownIds() => Oracle.Select(e=>(string)e["id"]);

        [TestCaseSource(nameof(BrownIds))]
        public void ActualParserMatchesIndependentlyReviewedPrintedBrownFields(string id)
        {
            var e=Enemy(id);var expected=Oracle.Single(x=>(string)x["id"]==id);
            Assert.That(e.TokenColor,Is.EqualTo(EnemyTokenColor.Brown));
            Assert.That(e.Armor,Is.EqualTo((int)expected["armor"]));
            Assert.That(e.ElusiveArmor,Is.EqualTo((int)expected["elusive"]));
            var abilities=((JArray)expected["abilities"]).Select(a=>(string)a).Select(a=>a switch {
                "swift"=>Ability.Swift,"poison"=>Ability.Poison,"paralyze"=>Ability.Paralyze,
                "brutal"=>Ability.Brutal,"magic resist"=>Ability.MagicResist,
                "cumbersome"=>Ability.Cumbersome,"vampirism"=>Ability.Vampirism,
                _=>throw new InvalidDataException("Unreviewed original icon: "+a) }).ToList();
            abilities.AddRange(((JArray)expected["resist"]).Select(r=>(string)r).Select(r=>r switch {
                "Physical"=>Ability.PhysicalResist,"Fire"=>Ability.FireResist,"Ice"=>Ability.IceResist,
                _=>throw new InvalidDataException("Unreviewed original shield: "+r) }));
            if((bool)expected["fortified"])abilities.Add(Ability.Fortified);
            if(e.ElusiveArmor>0)abilities.Add(Ability.Elusive);
            Assert.That(e.Abilities,Is.EquivalentTo(abilities));
            var attacks=(JArray)expected["attacks"];
            Assert.That(e.AttackCount,Is.EqualTo(attacks.Count));
            for(int i=0;i<attacks.Count;i++)
            {
                Assert.That(e.GetAttack(i).Value,Is.EqualTo((int)attacks[i][0]));
                Assert.That(e.GetAttack(i).Element.ToString(),Is.EqualTo((string)attacks[i][1]));
            }
        }

        [Test] public void OriginalCumbersomeAcceptsActualAgilityButKeepsAttackModePhaseRestricted()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Root,"resources/text_json"));
            var enemy=Enemy("monster_053");var player=new PlayerState(0,"test");
            var card=new DeedCard("basic_card_007",CardType.Action);player.Deck.Hand.Add(card);
            var battle=new PveBattleSession(player,new[]{enemy},CardJsonLoader.LoadBasicActions());
            Assert.That(battle.Choices(card).Any(c=>c.Id=="base"),Is.False);
            battle.Advance();
            Assert.That(battle.Choices(card).Any(c=>c.Id=="base"),Is.True);
            Assert.That(battle.Choices(card).Any(c=>c.Id=="strong"),Is.False);
            battle.Play(card,"base");
            Assert.That(battle.Context.MovementPool,Is.EqualTo(2));
            NatureRevengeActions.SpendMove(battle.Context,enemy,0,2);
            Assert.That(battle.Context.MovementPool,Is.Zero);
            Assert.That(CardCombatActions.DefenseProfile(enemy,0,battle.Context).Attack,Is.EqualTo(2));
            Assert.That(player.Deck.PlayedCards.Single(),Is.SameAs(card));
        }

        [Test] public void OriginalCumbersomeAcceptsActualMarchAndConsumesMovementPerAttack()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Root,"resources/text_json"));
            var e=Enemy("monster_053");var p=new PlayerState(0,"test");
            var first=new DeedCard("basic_card_000",CardType.Action);var second=new DeedCard("basic_card_000",CardType.Action);
            p.Deck.Hand.Add(first);p.Deck.Hand.Add(second);
            var b=new PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions());b.Advance();
            b.Play(first,"base");b.Play(second,"base");
            Assert.That(b.Context.MovementPool,Is.EqualTo(4));
            NatureRevengeActions.SpendMove(b.Context,e,0,4);
            Assert.That(b.Context.MovementPool,Is.Zero);Assert.That(p.Deck.PlayedCards,Is.EqualTo(new[]{first,second}));
            Assert.That(CardCombatActions.DefenseProfile(e,0,b.Context).Attack,Is.Zero);
            Assert.That(b.Context.SuccessfullyBlockedAttacks,Does.Contain((0,0)));
            b.Advance();b.Advance();Assert.That(p.Wounds,Is.Zero);
            Assert.That(b.Context.DefeatedEnemyIndices,Is.Empty);
        }

        [Test] public void OriginalThreeSlotCumbersomeReportsEveryZeroAttackThroughFormalSession()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Root,"resources/text_json"));
            var enemy=Enemy("monster_048");var player=new PlayerState(0,"test");
            var first=new DeedCard("basic_card_000",CardType.Action);
            var second=new DeedCard("basic_card_000",CardType.Action);
            player.Deck.Hand.Add(first);player.Deck.Hand.Add(second);
            var battle=new PveBattleSession(player,new[]{enemy},CardJsonLoader.LoadBasicActions());
            battle.Advance();battle.Play(first,"base");battle.Play(second,"base");
            NatureRevengeActions.SpendMove(battle.Context,enemy,1,1);
            Assert.That(battle.IsBlocked(0,1),Is.True,"The exact PPO failure was the second native attack");
            Assert.That(battle.IsBlocked(0,0),Is.False);Assert.That(battle.IsBlocked(0,2),Is.False);
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(battle.Context,enemy,1,1));
            NatureRevengeActions.SpendMove(battle.Context,enemy,0,1);
            NatureRevengeActions.SpendMove(battle.Context,enemy,2,1);
            Assert.That(Enumerable.Range(0,3).All(slot=>battle.IsBlocked(0,slot)),Is.True);
            Assert.That(battle.Context.MovementPool,Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards,Is.EqualTo(new[]{first,second}));
            battle.Advance();battle.Advance();Assert.That(player.Wounds,Is.Zero);
        }

        [Test] public void OriginalVampireActualHandWoundsIncreaseOnlySamePhysicalEnemyArmorUntilBattleEnd()
        {
            var e=Enemy("monster_045");var other=e with{};var p=new PlayerState(0,"test"){Armor=2};
            var c=new ActionContext {InBattle=true,CombatPhase=Phase.AssignDamage,Enemies=new List<Monster>{e,other}};
            c.SkipAttackIndices.Add(1);
            var result=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(result.TotalWounds,Is.EqualTo(3));Assert.That(p.Deck.Hand.Count(x=>x.Type==CardType.Wound),Is.EqualTo(3));
            Assert.That(c.Vampirism.Armor(e),Is.EqualTo(3));Assert.That(c.Vampirism.Armor(other),Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(e,0,c,Phase.Melee),Is.EqualTo(8));
            UnitDamageResolver.EndBattle(p,c);Assert.That(c.Vampirism.Armor(e),Is.Zero);
        }

        [Test] public void OriginalVampireBlockedAttackUsesPrintedZeroLowArmorWithoutWoundsOrArtificialIncrease()
        {
            var e=Enemy("monster_045");var p=new PlayerState(0,"test");
            var c=new ActionContext {InBattle=true,CombatPhase=Phase.AssignDamage,Enemies=new List<Monster>{e}};
            var result=BattleResolver.Resolve(p,c.Enemies,new[]{new BlockAllocation(0,5,Element.Physical)},Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(result.TotalWounds,Is.Zero);Assert.That(c.Vampirism.Armor(e),Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(e,0,c,Phase.Melee),Is.Zero);
            c.ArmorReduction[0]=2;Assert.That(CombatMath.EffectiveArmor(e,0,c,Phase.Melee),Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalVampireUnitGainDependsOnActualWoundNotResistanceAbsorption(bool resistant)
        {
            var e=Enemy("monster_045");var p=new PlayerState(0,"test");
            var unit=new UnitState {Card=UnitJsonLoader.Load(Path.Combine(Root,"resources/text_json"),"army_000")};p.Units.Add(unit);
            var c=new ActionContext {InBattle=true,Enemies=new List<Monster>{e},UnitsResistAll=resistant};
            int armor=unit.Card.Armor;
            UnitDamageResolver.Assign(p,c,unit,e,armor,Element.Physical);
            Assert.That(unit.Wounds,Is.EqualTo(resistant?0:1));
            Assert.That(c.Vampirism.Armor(e),Is.EqualTo(resistant?0:1));
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,unit,e,armor,Element.Physical));
            Assert.That(c.Vampirism.Armor(e),Is.EqualTo(resistant?0:1));
        }

        [Test] public void PrintedCrossedTowerIgnoresSiteFortificationAndRetainsSwiftDoubleArmor()
        {
            var e=Enemy("monster_017");var c=new ActionContext {FortifiedSite=true};
            Assert.That(e.Abilities,Does.Contain(Ability.IgnoreSiteFortification));
            Assert.That(e.Abilities,Does.Contain(Ability.Swift));Assert.That(e.ElusiveArmor,Is.EqualTo(8));
            Assert.That(CombatMath.IsFortified(e,0,c),Is.False);
        }

        [Test] public void FormalVampireDamagePlanRecordsUnitAndHandWoundsOnActualEnemyNotProfileCopy()
        {
            var e=Enemy("monster_045");var p=new PlayerState(0,"test"){Armor=2};
            var unit=new UnitState {Card=UnitJsonLoader.Load(Path.Combine(Root,"resources/text_json"),"army_000")};p.Units.Add(unit);
            var c=new ActionContext {InBattle=true,CombatPhase=Phase.AssignDamage,Enemies=new List<Monster>{e}};
            var result=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),
                ctx:c,damageAssignments:new[]{new UnitDamageAllocation(0,new[]{unit})});
            int handWounds=(int)Math.Ceiling(Math.Max(0,e.Attack-unit.Card.Armor)/2.0);
            Assert.That(unit.Wounds,Is.EqualTo(1));Assert.That(result.TotalWounds,Is.EqualTo(handWounds));
            Assert.That(c.Vampirism.Armor(e),Is.EqualTo(1+handWounds));
            Assert.That(c.Vampirism.Armor(e with{}),Is.Zero);
            Assert.That(CombatMath.EffectiveArmor(e,0,c,Phase.Melee),Is.EqualTo(5+1+handWounds));
        }

        [Test] public void IgnoredOriginalVampirismStillWoundsButGrantsNoArmor()
        {
            var e=Enemy("monster_045");var p=new PlayerState(0,"test"){Armor=2};
            var c=new ActionContext {InBattle=true,CombatPhase=Phase.AssignDamage,Enemies=new List<Monster>{e}};
            c.IgnoredAbilities[0]=Ability.Vampirism;
            var result=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(result.TotalWounds,Is.EqualTo(3));Assert.That(c.Vampirism.Armor(e),Is.Zero);
        }

        [TestCase("monster_045")] [TestCase("monster_053")]
        public void ActualOriginalColdStareCardRemovesNewPrintedAttackAbilities(string id)
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Root,"resources/text_json"));
            var e=Enemy(id);var p=new PlayerState(0,"test"){Armor=2};
            var c=new ActionContext {InBattle=true,CombatPhase=Phase.Block,Enemies=new List<Monster>{e},
                SelectedEnemy=e,SelectedEnemyAttackIndex=0};
            var card=new DeedCard("advanced_card_043",CardType.Action);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(a=>a.Id==card.Id),option:1);
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            var profile=CardCombatActions.DefenseProfile(e,0,c);
            Assert.That(profile.Abilities.Contains(Ability.Vampirism),Is.False);
            Assert.That(profile.Abilities.Contains(Ability.Cumbersome),Is.False);
            if(id=="monster_053")
                Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(c,e,0,1));
            c.CombatPhase=Phase.AssignDamage;
            BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(p.Wounds,Is.GreaterThan(0));Assert.That(c.Vampirism.Armor(e),Is.Zero);
        }
    }
}
