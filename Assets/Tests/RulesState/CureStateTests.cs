using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CureStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Cast(PlayerState p, ActionContext c, int option = 0)
        {
            var spell = CardJsonLoader.LoadSpells().Single(x => x.Id == "magic_001");
            var card = new DeedCard(spell.Id, CardType.Spell); p.Deck.Hand.Add(card);
            var data = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath, spell.EnImagePath,
                spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            CardPlayActions.Play(p, c, card, data, false, option);
            return card;
        }

        private static void DrawPile(PlayerState p, int count)
        {
            for (int i = 0; i < count; i++) p.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
        }

        [Test]
        public void ProvidesTwoPointsAndDrawsOnlyWhenActualWoundsAreHealed()
        {
            var p = new PlayerState { Wounds = 3 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green); DrawPile(p, 4);
            Cast(p, c);
            Assert.That(p.Wounds, Is.EqualTo(3)); Assert.That(c.Healing.Total, Is.EqualTo(2)); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(4));
            CardHealing.Spend(p, c); Assert.That(p.Wounds, Is.EqualTo(2)); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(3));
            CardHealing.Spend(p, c); Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(2));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero); Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(1));
        }

        [Test]
        public void EarlierHandHealingThisTurnDrawsRetroactivelyOncePerCast()
        {
            var p = new PlayerState { Wounds = 2 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green, 2); DrawPile(p, 6);
            c.Healing.Add(1); CardHealing.Spend(p, c);
            Cast(p, c); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(5));
            Cast(p, c); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(4));
            CardHealing.Spend(p, c);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(2)); Assert.That(p.Wounds, Is.Zero); Assert.That(c.DrawPerHeal, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalUnitHealedBeforeOrAfterCastReadiesEvenWithAnotherWound(bool before)
        {
            var p = new PlayerState(); var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green); DrawPile(p, 3);
            var u = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); u.AddWounds(2); u.Exhaust(); p.Units.Add(u);
            if (before) { c.Healing.Add(1); CardHealing.Spend(p, c, unit: u); Assert.That(u.IsReady, Is.False); }
            Cast(p, c);
            if (!before) CardHealing.Spend(p, c, unit: u);
            Assert.That(u.Wounds, Is.EqualTo(1)); Assert.That(u.IsReady, Is.True); Assert.That(u.CanActivate, Is.False);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(3), "Unit healing never draws hand cards.");
        }

        [Test]
        public void NoWoundNoDrawAndRejectedHealingDoesNotConsumePoints()
        {
            var p = new PlayerState(); var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green); DrawPile(p, 2);
            Cast(p, c); Assert.Throws<InvalidOperationException>(() => CardHealing.Spend(p, c));
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(2)); Assert.That(c.Healing.Total, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void GrailAndCureDrawRewardsStackInEitherOrder(bool grailFirst)
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green); DrawPile(p, 5);
            if (grailFirst) new HolyGrailEffect(true).Execute(p, c);
            Cast(p, c);
            if (!grailFirst) new HolyGrailEffect(true).Execute(p, c);
            CardHealing.Spend(p, c);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(3)); Assert.That(c.Healing.Total, Is.EqualTo(7)); Assert.That(c.DrawPerHeal, Is.EqualTo(2));
        }

        [Test]
        public void NewWoundDrawnByHealingCanBeHealedAgain()
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green);
            p.Deck.PutUnder(new DeedCard("w", CardType.Wound)); p.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
            Cast(p, c); CardHealing.Spend(p, c); Assert.That(p.Wounds, Is.EqualTo(1));
            CardHealing.Spend(p, c); Assert.That(p.Wounds, Is.Zero); Assert.That(p.Deck.DrawPileCount, Is.Zero);
        }

        [Test]
        public void TurnEndClearsHistoryAndRewardBeforeNextCure()
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext { SkipDraw = true }; DrawPile(p, 4);
            var u = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); u.AddWounds(1); u.Exhaust(); p.Units.Add(u);
            c.Healing.Add(2); CardHealing.Spend(p, c); CardHealing.Spend(p, c, unit: u);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            p.Mana.AddToken(ManaColor.Green); Cast(p, c);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(4)); Assert.That(u.IsReady, Is.False);
            c.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(c.DrawPerHeal, Is.Zero); Assert.That(c.ReadyUnitOnHeal, Is.False); Assert.That(c.Healing.Total, Is.Zero);
        }

        [Test]
        public void BattleRejectsBeforeGreenPaymentOrHandMutation()
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext { InBattle = true }; p.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(() => Cast(p, c));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(c.DrawPerHeal, Is.Zero);
        }

        [Test]
        public void MissingGreenCannotGrantPointsOrRewards()
        {
            var p = new PlayerState(); var c = new ActionContext();
            Assert.Throws<InvalidOperationException>(() => Cast(p, c));
            Assert.That(c.Healing.Total, Is.Zero); Assert.That(c.DrawPerHeal, Is.Zero); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }
    }
}
