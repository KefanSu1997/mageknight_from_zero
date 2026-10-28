using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class JoyfulManaBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [TestCase(false)]
        [TestCase(true)]
        public void PrintedCostAndCrystalThenActualReturnBeforeRefill(bool enhanced)
        {
            var player = new PlayerState();
            var context = new ActionContext();
            var original = CardJsonLoader.LoadBasicActions().Single(c => c.Id == "basic_card_019");
            var source = new DeedCard(original.Id, CardType.Action);
            var payment = enhanced
                ? new DeedCard("w", CardType.Wound)
                : new DeedCard("basic_card_021", CardType.Action);
            var nextDraw = new DeedCard("basic_card_016", CardType.Action);
            player.Deck.Hand.AddRange(new[] { source, payment });
            player.Deck.PutOnTop(nextDraw);
            player.Mana.AddToken(enhanced ? ManaColor.Blue : ManaColor.Red);

            CardPlayActions.Play(player, context, source, original, enhanced, (int)ManaColor.Red);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(payment));

            CardPlayActions.SelectJoyfulManaReturn(player, context, source, payment);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(payment));

            new PlayerTurnEngine().EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand[0], Is.SameAs(source));
            Assert.That(player.Deck.Hand[1], Is.SameAs(nextDraw));
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(payment));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DrawPile, Is.Empty);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
        }

        [Test]
        public void BaseCannotDiscardWoundToReturnAndCanDeclineTheReturn()
        {
            var player = new PlayerState();
            var context = new ActionContext { SkipDraw = true };
            var original = CardJsonLoader.LoadBasicActions().Single(c => c.Id == "basic_card_019");
            var source = new DeedCard(original.Id, CardType.Action);
            var wound = new DeedCard("w", CardType.Wound);
            player.Deck.Hand.AddRange(new[] { source, wound });
            player.Mana.AddToken(ManaColor.Red);
            CardPlayActions.Play(player, context, source, original, false, (int)ManaColor.Red);

            Assert.Throws<InvalidOperationException>(() =>
                CardPlayActions.SelectJoyfulManaReturn(player, context, source, wound));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(wound));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));

            new PlayerTurnEngine().EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(wound));
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(source));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
        }
    }
}
