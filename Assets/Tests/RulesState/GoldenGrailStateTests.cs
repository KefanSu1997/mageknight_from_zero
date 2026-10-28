using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class GoldenGrailStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void PlayingGrailDoesNotChooseHeroInsteadOfUnit()
        {
            var p = new PlayerState { Wounds = 3 }; var c = new ActionContext();
            new HolyGrailEffect(false).Execute(p, c);
            Assert.That(p.Wounds, Is.EqualTo(3), "Healing must wait for an actual selected target.");
            Assert.That(p.Fame, Is.Zero, "Unspent healing earns no Fame.");
            Assert.That(c.Healing.Total, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void DrawRewardExpiresEvenWhenRefillIsSkipped(bool skip)
        {
            var p = new PlayerState(); var c = new ActionContext { SkipDraw = skip };
            new HolyGrailEffect(true).Execute(p, c);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            p.Wounds = 1; p.Deck.PutOnTop(new DeedCard("basic_card_000", CardType.Action));
            CardHealing.Heal(p, c, 1);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(1));
            Assert.That(c.DrawPerHeal, Is.Zero);
        }

        private static DeedCard Play(PlayerState p, ActionContext c, bool burn = false)
        {
            var card = new DeedCard("items_012", CardType.Artifact); p.Deck.Hand.Add(card);
            CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == card.Id), burn);
            return card;
        }

        private static UnitState Own(PlayerState p, string id, int wounds = 1)
        {
            var u = new UnitState(CardJsonLoader.LoadUnit(id)); p.Units.Add(u); u.Exhaust(); u.AddWounds(wounds); return u;
        }

        [Test]
        public void BaseSpendsTwoPointsOnLevelTwoUnitForTwoFameWithoutReadying()
        {
            var p = new PlayerState { Wounds = 3 }; var c = new ActionContext();
            var u = Own(p, "army_2_001"); var card = Play(p, c);
            CardHealing.Spend(p, c, unit: u);
            Assert.That(p.Fame, Is.EqualTo(2)); Assert.That(p.Wounds, Is.EqualTo(3));
            Assert.That(u.Wounds, Is.Zero); Assert.That(u.IsReady, Is.False); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card)); Assert.That(p.Fame, Is.EqualTo(2));
        }

        [Test]
        public void BaseSplitsHandAndLevelOneUnitAndCannotHealDiscard()
        {
            var p = new PlayerState { Wounds = 1, DiscardWounds = 1 }; var c = new ActionContext();
            var u = Own(p, "army_2_006"); Play(p, c);
            CardHealing.Spend(p, c); Assert.That(p.Fame, Is.EqualTo(1));
            CardHealing.Spend(p, c, unit:u);
            Assert.That(p.Fame, Is.EqualTo(2)); Assert.That(p.DiscardWounds, Is.EqualTo(1));
            Assert.That(u.Wounds, Is.Zero); Assert.That(p.Wounds, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardHealing.Spend(p,c));
        }

        [Test]
        public void StrongHealsTwoPoisonWoundsAtLevelCostWithoutHandDraw()
        {
            var p = new PlayerState(); var c = new ActionContext(); var u = Own(p,"army_2_001",2);
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            var card=Play(p,c,true); CardHealing.Spend(p,c,2,u);
            Assert.That(u.Wounds,Is.Zero); Assert.That(u.IsReady,Is.False);
            Assert.That(c.Healing.Total,Is.EqualTo(2)); Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));
            Assert.That(p.Fame,Is.Zero); Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(card));
        }

        [Test]
        public void BurnDrawsForEachActualHandWoundIncludingNewlyDrawnWounds()
        {
            var p=new PlayerState{Wounds=1}; var c=new ActionContext();
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            p.Deck.PutOnTop(new DeedCard("w",CardType.Wound)); Play(p,c,true);
            CardHealing.Spend(p,c); Assert.That(p.Wounds,Is.EqualTo(1));
            CardHealing.Spend(p,c); Assert.That(p.Wounds,Is.Zero);
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo("basic_card_000"));
            Assert.That(c.Healing.Total,Is.EqualTo(4)); Assert.That(p.Deck.DrawPileCount,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardHealing.Spend(p,c));
            Assert.That(c.Healing.Total,Is.EqualTo(4));
        }

        [Test]
        public void BurnDrawRewardAlsoAppliesToOtherHealingThisTurn()
        {
            var p=new PlayerState{Wounds=1}; var c=new ActionContext(); Play(p,c,true);
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            new HealOrDrawEffect(1,1).Execute(p,c);
            CardHealing.Spend(p,c);
            Assert.That(p.Deck.Hand.Single().Id,Is.EqualTo("basic_card_000"));
            Assert.That(c.Healing.Total,Is.EqualTo(6)); Assert.That(p.Fame,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void SourceSpecificFameCannotRewardOrdinaryHealing(bool grailFirst)
        {
            var p=new PlayerState{Wounds=3}; var c=new ActionContext(); Play(p,c);
            c.Healing.Add(2); // The shared pool preserves separately granted ordinary healing.
            CardHealing.Spend(p,c,2,grailFirst:grailFirst);
            Assert.That(p.Fame,Is.EqualTo(grailFirst?2:0));
            Assert.That(c.Healing.Grail,Is.EqualTo(grailFirst?0:2));
            Assert.That(c.Healing.Total,Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidTargetsOrCostsDoNotSpendOrHeal(int failure)
        {
            var p=new PlayerState(); var c=new ActionContext(); var u=Own(p,"army_2_001",2); Play(p,c);
            int wounds=1;
            if(failure==0)p.Units.Remove(u);
            if(failure==1)u.Destroy();
            if(failure==2)wounds=2;
            if(failure==3)wounds=0;
            if(failure==4)u.Heal(2);
            int before=u.Wounds;
            Assert.Throws<InvalidOperationException>(()=>CardHealing.Spend(p,c,wounds,u));
            Assert.That(u.Wounds,Is.EqualTo(before)); Assert.That(c.Healing.Total,Is.EqualTo(2)); Assert.That(p.Fame,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void CombatRejectsGrailWithoutMovingCard(bool burn)
        {
            var p=new PlayerState{Wounds=1}; var c=new ActionContext{InBattle=true};
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,burn));
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(2)); Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards,Is.Empty); Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(c.Healing.Total,Is.Zero); Assert.That(c.DrawPerHeal,Is.Zero);
        }

        [Test]
        public void CombatClearsUnspentPointsButTurnDrawRewardLastsUntilTurnEnd()
        {
            var p=new PlayerState{Wounds=1};var c=new ActionContext();Play(p,c,true);
            c.InBattle=true;Assert.That(c.Healing.Total,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardHealing.Spend(p,c));
            c.InBattle=false; Assert.That(c.DrawPerHeal,Is.EqualTo(1));
            p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action)); CardHealing.Heal(p,c,1);
            Assert.That(p.Deck.Hand.Count,Is.EqualTo(1));
            c.SkipDraw=true; new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(c.DrawPerHeal,Is.Zero);
        }
    }
}
