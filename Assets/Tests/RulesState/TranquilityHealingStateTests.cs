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
    public sealed class TranquilityHealingStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState p, ActionContext c, bool strong=false, int option=0)
        {
            var card=new DeedCard("basic_card_001",CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),strong,option);
            return card;
        }

        [TestCase(false)] [TestCase(true)]
        public void HealingWaitsForTargetAfterExactOriginalCost(bool strong)
        {
            var p=new PlayerState{Wounds=3};var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            var card=Play(p,c,strong);
            Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(c.Healing.Total,Is.EqualTo(strong?2:1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.EqualTo(strong?0:1));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }

        [TestCase(-1)] [TestCase(2)]
        public void InvalidChoiceDoesNotPayOrDraw(int option)
        {
            var p=new PlayerState();var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true,option));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase(false,0)] [TestCase(false,1)] [TestCase(true,0)] [TestCase(true,1)]
        public void HealingIconDisallowsBothChoicesDuringCombat(bool strong,int option)
        {
            var p=new PlayerState{Wounds=1}; var c=new ActionContext{InBattle=true};p.Mana.AddToken(ManaColor.Green);
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong,option));
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void DrawChoiceDoesNotHealOrGrantPointsAndDoesNotReshuffle(bool strong)
        {
            var p=new PlayerState{Wounds=2};var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            p.Deck.GainToDiscard(new DeedCard("basic_card_014",CardType.Action));
            Play(p,c,strong,1);
            Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(c.Healing.Total,Is.Zero);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(3));Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(1));
        }

        [Test]
        public void GrailAndTranquilityCombineForOriginalLevelThreeUnitButOnlyGrailGivesFame()
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext();
            var unit=new UnitState(CardJsonLoader.LoadUnit("army_003"));unit.AddWounds(1);unit.Exhaust();p.Units.Add(unit);
            var grail=new DeedCard("items_012",CardType.Artifact);p.Deck.Hand.Add(grail);
            CardPlayActions.PlayArtifact(p,c,grail,CardJsonLoader.LoadItems().Single(x=>x.Id==grail.Id));
            Assert.Throws<InvalidOperationException>(()=>CardHealing.Spend(p,c,unit:unit));
            Assert.That(c.Healing.Total,Is.EqualTo(2));
            Play(p,c);CardHealing.Spend(p,c,unit:unit);
            Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsReady,Is.False);Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Fame,Is.EqualTo(2));Assert.That(p.Deck.PlayedCards.Count,Is.EqualTo(2));
        }

        [Test]
        public void EnhancedHealingCanBeSplitBetweenHandAndLevelOneUnit()
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext();p.Mana.AddToken(ManaColor.Green);
            var u=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));p.Units.Add(u);u.AddWounds(1);u.Exhaust();
            Play(p,c,true);CardHealing.Spend(p,c);CardHealing.Spend(p,c,unit:u);
            Assert.That(p.Wounds,Is.Zero);Assert.That(u.Wounds,Is.Zero);Assert.That(u.IsReady,Is.False);
            Assert.That(p.Fame,Is.Zero);Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void UnspentPointsExpireAndActualCardDiscards(bool skip)
        {
            var p=new PlayerState();var c=new ActionContext{SkipDraw=skip};var card=Play(p,c);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(card));
        }

        [TestCase(0)] [TestCase(1)]
        public void MissingGreenCannotGrantEitherEnhancedBenefit(int option)
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext();p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true,option));
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
    }
}
