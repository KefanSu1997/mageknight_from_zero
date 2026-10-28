using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RoundDeckLifecycleTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void EndRoundReturnsActualHandWoundsAndRemainingDeckWithoutRestoringRemovedCards()
        {
            var p = new PlayerState();
            var kept = new DeedCard("basic_card_000", CardType.Action);
            var reserve = new DeedCard("basic_card_016", CardType.Action);
            var discarded = new DeedCard("basic_card_021", CardType.Action);
            var burned = new DeedCard("items_003", CardType.Artifact);
            p.Deck.Hand.AddRange(new[] { kept, burned });
            Assert.That(p.Deck.TryRemovePermanentlyExact(burned), Is.True);
            p.Deck.GainWound();
            var wound = p.Deck.Hand.Single(c => c.Type == CardType.Wound);
            long woundsReceived = p.Deck.HandWoundsReceived;
            p.Deck.PutOnTop(reserve); p.Deck.GainToDiscard(discarded);
            new GameEngine(new[] { p }, 3).EndRound();
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(4));
            foreach (var card in new[] { kept, reserve, discarded, wound })
                Assert.That(p.Deck.DrawPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(burned));
            Assert.That(p.Deck.HandWoundsReceived, Is.EqualTo(woundsReceived));
        }

        [Test]
        public void SamePrintedCardCopiesKeepTheirDistinctPhysicalInstancesAcrossRounds()
        {
            var p = new PlayerState();
            var first = new DeedCard("basic_card_000", CardType.Action);
            var second = new DeedCard("basic_card_000", CardType.Action);
            p.Deck.Hand.Add(first); p.Deck.GainToDiscard(second);
            var game = new GameEngine(new[] { p }, 3);
            for (int round = 0; round < 2; round++)
            {
                game.EndRound();
                Assert.That(p.Deck.DrawPile.Count(c => ReferenceEquals(c, first)), Is.EqualTo(1));
                Assert.That(p.Deck.DrawPile.Count(c => ReferenceEquals(c, second)), Is.EqualTo(1));
                // This test exercises round refill, not tactics or initiative.
                game.StartNewRound(Array.Empty<TacticCard>());
                Assert.That(p.Deck.Hand.Count, Is.EqualTo(2));
                Assert.That(p.Deck.Hand.Count(c => ReferenceEquals(c, first)), Is.EqualTo(1));
                Assert.That(p.Deck.Hand.Count(c => ReferenceEquals(c, second)), Is.EqualTo(1));
            }
        }

        [Test]
        public void OrdinaryTurnKeepsUnplayedCardsAndOnlyRoundBoundaryCollectsThem()
        {
            var p = new PlayerState();
            var kept = new DeedCard("basic_card_000", CardType.Action);
            var played = new DeedCard("basic_card_021", CardType.Action);
            p.Deck.Hand.AddRange(new[] { kept, played });
            Assert.That(p.Deck.TryPutInPlay(played), Is.True);
            var game = new GameEngine(new[] { p }, 3);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, game.Globals.Mana, new ActionContext { SkipDraw = true });
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(kept));
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(played));
            game.EndRound();
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.DrawPile.Count(c => ReferenceEquals(c, kept)), Is.EqualTo(1));
            Assert.That(p.Deck.DrawPile.Count(c => ReferenceEquals(c, played)), Is.EqualTo(1));
        }
    }
}
