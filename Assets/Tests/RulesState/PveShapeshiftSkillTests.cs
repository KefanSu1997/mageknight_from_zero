using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveShapeshiftSkillTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private SkillCard Skill(PlayerState p){var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[6];p.Skills.Add(s);return s;}
        private DeedCard Card(PlayerState p,int id){var c=new DeedCard("basic_card_"+id.ToString("000"),CardType.Action);p.Deck.Hand.Add(c);return c;}
        private PveBattleSession Battle(PlayerState p,string enemy="monster_032") =>new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id==enemy),CardJsonLoader.LoadBasicActions());
        [Test] public void PrintedStrongMarchPaysGreenAndBlocksActualEnemy()
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,0);p.Mana.AddCrystal(ManaColor.Green);var s=Battle(p);s.Advance();
            ShapeshiftActions.Play(p,s.Context,skill,card,true,0,FixedOutputKind.Block);
            Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(s.Context.MovementPool,Is.Zero);Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(4));
            s.Block(0);s.Advance();s.Advance();Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }
        [Test] public void FuryMoveConsumesFormalMapCostAndNextCardIsNotConverted()
        {
            var p=new PlayerState{HeroId="Braevalar",Fame=3};var skill=Skill(p);
            p.Deck.SetDeck(Enumerable.Range(0,8).Select(_=>new DeedCard("basic_card_021",CardType.Action)));
            var map=new MapState();foreach(var pos in new[]{new AxialCoord(0,0),new AxialCoord(1,0)})map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);j.SelectTactic("day_1");j.BeginTurn();var card=p.Deck.Hand[0];
            ShapeshiftActions.Play(p,j.Context,skill,card,false,0,FixedOutputKind.Movement);j.Move(new AxialCoord(1,0));
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.That(p.Position,Is.EqualTo(new AxialCoord(1,0)));
            var next=p.Deck.Hand[0];CardPlayActions.Play(p,j.Context,next,CardJsonLoader.LoadBasicActions().Single(c=>c.Id==next.Id));
            Assert.That(j.Context.MeleePool,Is.EqualTo(2));Assert.That(SkillActions.IsUsed(p,skill),Is.True);
        }
        [Test] public void IceBlockBecomesIceAttackAndActualResistanceReducesIt()
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,20);var s=Battle(p,"monster_004");s.Advance();s.Advance();s.Advance();
            ShapeshiftActions.Play(p,s.Context,skill,card,false,1,FixedOutputKind.Attack);
            var a=s.Context.CombatPower.Attacks.Single();Assert.That(a.Value,Is.EqualTo(3));Assert.That(a.Element,Is.EqualTo(Element.Ice));Assert.That(a.SourceCard,Is.SameAs(card));
            Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(1));
        }
        [Test] public void StrongIcyShellConvertsOnlyFiveAndRetainsDynamicEnemySymbols()
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,20);p.Mana.AddCrystal(ManaColor.Blue);var s=Battle(p,"monster_029");s.Advance();s.Advance();s.Advance();
            ShapeshiftActions.Play(p,s.Context,skill,card,true,0,FixedOutputKind.Attack);
            Assert.That(s.Context.CombatPower.Attacks.Single().Value,Is.EqualTo(5));var block=s.Context.CombatPower.Blocks.Single();
            Assert.That(block.Value,Is.Zero);Assert.That(block.CountEnemySymbols,Is.True);Assert.That(block.Element,Is.EqualTo(Element.Ice));
            Assert.That(CombatMath.ResolveBlocks(s.Enemies[0],new[]{block}).Single().Value,Is.GreaterThan(0));
        }
        [TestCase(false,3)] [TestCase(true,5)]
        public void ImprovisationStillDiscardsExactMaterialAndPaysOriginalCost(bool enhanced,int value)
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,23);var material=Card(p,8);p.Mana.AddCrystal(ManaColor.Red);var s=Battle(p);s.Advance();
            ShapeshiftActions.Play(p,s.Context,skill,card,enhanced,0,FixedOutputKind.Block,material);
            Assert.That(s.Context.BlockPool,Is.EqualTo(value));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
            Assert.That(p.Mana.GetTotal(),Is.EqualTo(enhanced?0:1));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidFeeMaterialPhaseOrNonBasicPreservesUsageCardAndMana(int bad)
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,bad==1?23:0);var s=Battle(p);if(bad!=2)s.Advance();
            if(bad==3){card=new DeedCard(CardJsonLoader.LoadAdvancedActions()[0].Id,CardType.Action);p.Deck.Hand.Add(card);}
            int hand=p.Deck.Hand.Count;
            Assert.Throws<InvalidOperationException>(()=>ShapeshiftActions.Play(p,s.Context,skill,card,bad==0,0,FixedOutputKind.Block));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(hand));Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(s.Context.BlockPool,Is.Zero);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [Test] public void FormalSessionEntryConvertsActualCardAndLeavesRageWoundRewardAsAttack()
        {
            var p=new PlayerState();var skill=Skill(p);var card=Card(p,25);var s=Battle(p);s.Advance();
            p.Deck.GainWound();
            s.PlayShapeshift(skill,card,false,0,FixedOutputKind.Block);
            Assert.That(s.Context.BlockPool,Is.EqualTo(2));Assert.That(s.Context.MeleePool,Is.EqualTo(1));
            Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }
        [Test] public void FormalJourneyEntryRejectsBattleOutputAndThenConsumesConvertedMovement()
        {
            var p=new PlayerState{HeroId="Braevalar",Fame=3};var skill=Skill(p);
            p.Deck.SetDeck(Enumerable.Range(0,8).Select(_=>new DeedCard("basic_card_021",CardType.Action)));
            var map=new MapState();foreach(var pos in new[]{new AxialCoord(0,0),new AxialCoord(1,0)})map.Placed.Add(pos,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(TerrainType.Plains,6).ToArray()));
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);j.SelectTactic("day_1");j.BeginTurn();var card=p.Deck.Hand[0];
            Assert.Throws<InvalidOperationException>(()=>j.PlayShapeshift(skill,card,false,0,FixedOutputKind.Block));
            j.PlayShapeshift(skill,card,false,0,FixedOutputKind.Movement);j.Move(new AxialCoord(1,0));
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.That(p.Position,Is.EqualTo(new AxialCoord(1,0)));
        }
        [Test] public void MetadataComesFromAllImplementedFixedBasicEffectOptions()
        {
            foreach(int id in new[]{0,3,4,7,10,11,14,16,17,18,20,21,23,24,25,27})
            foreach(bool enhanced in new[]{false,true})
                Assert.That(ShapeshiftActions.Choices(new DeedCard("basic_card_"+id.ToString("000"),CardType.Action),enhanced).Count,Is.GreaterThan(0),id+" "+enhanced);
            Assert.That(ShapeshiftActions.Choices(new DeedCard("basic_card_004",CardType.Action),true).Any(o=>o.Kind==FixedOutputKind.Block),Is.False,"地形动态格挡不能冒充固定项");
        }
    }
}
