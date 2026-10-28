using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RegenerationStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState p, ActionContext c, bool strong, int option = 0)
        {
            var card = new DeedCard("advanced_card_015", CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==card.Id),strong,option);
            return card;
        }

        private static UnitState Unit(PlayerState p,string id,int wounds=0)
        {
            var u=new UnitState(CardJsonLoader.LoadUnit(id));u.Exhaust();u.AddWounds(wounds);p.Units.Add(u);return u;
        }

        [TestCase(false)] [TestCase(true)]
        public void ReadiesSelectedUnitButHealingWaitsForSeparateTarget(bool strong)
        {
            var p=new PlayerState{Wounds=3};p.Mana.AddToken(ManaColor.Green);
            var ready=Unit(p,strong?"army_003":"army_2_001",1);
            var heal=Unit(p,strong?"army_2_001":"army_2_006",1);
            var c=new ActionContext{TargetUnit=ready};Play(p,c,strong);
            Assert.That(ready.IsReady,Is.True);Assert.That(ready.Wounds,Is.EqualTo(1));Assert.That(ready.CanActivate,Is.False);
            Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(c.Healing.Total,Is.EqualTo(strong?2:1));
            CardHealing.Spend(p,c,unit:heal);
            Assert.That(heal.Wounds,Is.Zero);Assert.That(heal.IsReady,Is.False);Assert.That(c.Healing.Total,Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.EqualTo(strong?0:1));
        }

        [TestCase(false)] [TestCase(true)]
        public void CanForgoReadyingAndSpendOnActualHandWounds(bool strong)
        {
            var p=new PlayerState{Wounds=3};p.Mana.AddToken(ManaColor.Green);var c=new ActionContext();
            Play(p,c,strong);Assert.That(p.Wounds,Is.EqualTo(3));CardHealing.Spend(p,c,strong?2:1);
            Assert.That(p.Wounds,Is.EqualTo(strong?1:2));Assert.That(c.HealedHandWoundsThisTurn,Is.EqualTo(strong?2:1));
            Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(1));Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void HealingThenActualUnitActivationUsesReadyState(bool strong)
        {
            var p=new PlayerState();p.Mana.AddToken(ManaColor.Green);var u=Unit(p,"army_2_006",1);
            var c=new ActionContext{TargetUnit=u};Play(p,c,strong);Assert.That(u.CanActivate,Is.False);
            CardHealing.Spend(p,c,unit:u);Assert.That(u.CanActivate,Is.True);
            Assert.That(c.Healing.Total,Is.EqualTo(strong?1:0));
            c.InBattle=true;CardUnitActions.Use(p,c,u,0);
            Assert.That(u.IsReady,Is.False);Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void CombatRejectsBeforeAnyEffectOrFee(bool strong)
        {
            var p=new PlayerState{Wounds=2};p.Mana.AddToken(ManaColor.Green);var u=Unit(p,"army_2_001");
            var c=new ActionContext{InBattle=true,TargetUnit=u};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong));
            Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(u.IsReady,Is.False);Assert.That(c.Healing.Total,Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase("foreign")] [TestCase("destroyed")] [TestCase("baseOverlevel")] [TestCase("strongOverlevel")]
        public void IllegalSelectedUnitRejectsAtomically(string problem)
        {
            var p=new PlayerState{Wounds=2};p.Mana.AddToken(ManaColor.Green);
            var u=Unit(p,problem=="baseOverlevel"?"army_003":"army_2_001");
            if(problem=="strongOverlevel")
            {
                // Invalid boundary fixture only; no level-IV original unit is currently loadable.
                p.Units.Clear();
                u=new UnitState(new UnitCard("boundary_level4","等级越界夹具","Fixture",4,4,6,RecruitLocation.Village,
                    Array.Empty<AttackProfile>(),Array.Empty<Ability>()));u.Exhaust();p.Units.Add(u);
            }
            if(problem=="foreign")p.Units.Remove(u);if(problem=="destroyed")u.Destroy();
            var c=new ActionContext{TargetUnit=u};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,problem!="baseOverlevel"));
            Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(c.Healing.Total,Is.Zero);Assert.That(u.IsReady,Is.False);
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase(-1)] [TestCase(1)]
        public void InvalidChoiceDoesNotSpendMana(int option)
        {
            var p=new PlayerState();p.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>Play(p,new ActionContext(),true,option));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [Test]
        public void MissingGreenLeavesUnitAndHandUnchanged()
        {
            var p=new PlayerState{Wounds=2};var u=Unit(p,"army_2_001");var c=new ActionContext{TargetUnit=u};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true));
            Assert.That(u.IsReady,Is.False);Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(c.Healing.Total,Is.Zero);
        }

        [Test]
        public void UnusedHealingExpiresAndPlayedSourceDiscards()
        {
            var p=new PlayerState();var c=new ActionContext{SkipDraw=true};var card=Play(p,c,false);
            Assert.That(c.Healing.Total,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,card,
                CardJsonLoader.LoadAdvancedActions().Single(x=>x.Id==card.Id)));
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }
    }
}
