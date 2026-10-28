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
    public sealed class GoldPouchStateTests
    {
        [SetUp]
        public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard card) Fixture()
        {
            var p = new PlayerState { Fame = 7, Reputation = 0, ExtraCommandSlots = 2 };
            var c = new ActionContext { SkipDraw = true };
            var card = new DeedCard("items_010", CardType.Artifact);
            p.Deck.Hand.Add(card);
            return (p, c, card);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool burn = false)
            => CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == "items_010"), burn);

        [TestCase(false, 4, 9)]
        [TestCase(true, 9, 10)]
        public void PrintedEffectsAddToPoolsWithoutManaOrTarget(bool burn, int influence, int fame)
        {
            var (p, c, card) = Fixture();
            c.InfluencePool = 2;
            p.Mana.AddToken(ManaColor.Red, 1);
            p.Mana.AddCrystal(ManaColor.Blue, 1);
            Play(p, c, card, burn);
            Assert.That(c.InfluencePool, Is.EqualTo(influence + 2));
            Assert.That(p.Fame, Is.EqualTo(fame));
            Assert.That(p.Reputation, Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1));
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(c.SpentCrystals.Count, Is.Zero);
            Assert.That(p.Deck.Hand.Count, Is.Zero);
            Assert.That(burn ? p.Deck.RemovedCards.Single() : p.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.That(burn ? p.Deck.PlayedCards.Count : p.Deck.RemovedCards.Count, Is.Zero);
        }

        [TestCase(false, 4, 9)]
        [TestCase(true, 9, 10)]
        public void SameInstanceCannotGenerateResourcesTwice(bool burn, int influence, int fame)
        {
            var (p, c, card) = Fixture();
            Play(p, c, card, burn);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card, burn));
            Assert.That(c.InfluencePool, Is.EqualTo(influence));
            Assert.That(p.Fame, Is.EqualTo(fame));
            Assert.That(burn ? p.Deck.RemovedCards.Count : p.Deck.PlayedCards.Count, Is.EqualTo(1));
        }

        [Test]
        public void BasicActualInstanceDiscardsThenReturnsNextRoundWhileInfluenceExpires()
        {
            var (p, c, card) = Fixture();
            Play(p, c, card);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(9));
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            var game = new GameEngine(new[] { p }, 3);
            game.EndRound();
            game.StartNewRound(Array.Empty<TacticCard>());
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card));
            Play(p, c, card);
            Assert.That(c.InfluencePool, Is.EqualTo(4));
            Assert.That(p.Fame, Is.EqualTo(11));
        }

        [Test]
        public void BurnedActualInstanceNeverReturnsAfterTurnOrShuffle()
        {
            var (p, c, card) = Fixture();
            Play(p, c, card, true);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            var game = new GameEngine(new[] { p }, 3);
            game.EndRound();
            game.StartNewRound(Array.Empty<TacticCard>());
            Assert.That(p.Deck.Hand.Count, Is.Zero);
            Assert.That(p.Deck.DiscardPile.Count(), Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(card));
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(10));
        }

        [Test]
        public void DuplicatePrintedCardsPreserveSelectedPhysicalInstancesAcrossBaseAndBurn()
        {
            var (p, c, first) = Fixture();
            var second = new DeedCard("items_010", CardType.Artifact);
            p.Deck.Hand.Add(second);
            Play(p, c, second, true);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(first));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(second));
            Play(p, c, first);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(first));
            Assert.That(c.InfluencePool, Is.EqualTo(13));
            Assert.That(p.Fame, Is.EqualTo(12));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(first));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(second));
        }

        [TestCase(false, 0, 9)]
        [TestCase(true, 5, 10)]
        public void InfluencePaysActualOriginalUnitRecruitmentAndKeepsImmediateFame(bool burn, int remainder, int fame)
        {
            var (p, c, card) = Fixture();
            var farmer = CardJsonLoader.LoadUnit("army_2_006");
            var offer = new List<UnitCard> { farmer };
            Play(p, c, card, burn);
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(p.Units.Single().Card, Is.SameAs(farmer));
            Assert.That(offer.Count, Is.Zero);
            Assert.That(c.InfluencePool, Is.EqualTo(remainder));
            Assert.That(p.Fame, Is.EqualTo(fame));
            CardUnitActions.Use(p, c, p.Units[0], 1);
            Assert.That(c.InfluencePool, Is.EqualTo(remainder + 2));
            Assert.That(p.Units[0].IsReady, Is.False);
        }

        [Test]
        public void InsufficientRecruitmentPreservesSupplyInfluenceAndAlreadyEarnedFame()
        {
            var (p, c, card) = Fixture();
            var thugs = CardJsonLoader.LoadUnit("army_2_011");
            var offer = new List<UnitCard> { thugs };
            Play(p, c, card);
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.False);
            Assert.That(offer.Single(), Is.SameAs(thugs));
            Assert.That(p.Units.Count, Is.Zero);
            Assert.That(c.InfluencePool, Is.EqualTo(4));
            Assert.That(p.Fame, Is.EqualTo(9));
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EqualLookingUnownedCardIsRejectedBeforeReward(bool burn)
        {
            var (p, c, owned) = Fixture();
            Assert.Throws<InvalidOperationException>(() => Play(p, c, new DeedCard("items_010", CardType.Artifact), burn));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(owned));
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(7));
            Assert.That(p.Deck.PlayedCards.Count + p.Deck.RemovedCards.Count, Is.Zero);
        }
    }
}
