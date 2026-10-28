using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RoundFlowBoundaryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static PlayerState PlayerWithCard()
        {
            var p = new PlayerState(); p.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action)); return p;
        }
        // These fixtures specify initiative only; they do not claim to implement original tactics effects/art.
        private static TacticCard[] Order(int count, DayPart part = DayPart.Day) => Enumerable.Range(1, count)
            .Select(i => new TacticCard("initiative_fixture_" + i, part, i, 0)).ToArray();

        [Test]
        public void DeclarationSkipsDeclarerAndPreservesCardsUntilAllOtherPlayersFinish()
        {
            var a = PlayerWithCard(); var b = PlayerWithCard();
            var aCard = a.Deck.Hand.Single(); var bCard = b.Deck.Hand.Single();
            var game = new GameEngine(new[] { a, b }, 3); game.StartNewRound(Order(2));
            a.TokensPerTurn[ManaColor.White] = 1;
            Assert.That(game.CanAnnounceRoundEnd, Is.True);
            game.AnnounceRoundEnd();
            Assert.That(game.HasActiveTurn, Is.False);
            Assert.That(game.ClockTracker.RoundEnded, Is.False);
            Assert.That(a.Mana.Tokens, Is.Empty); // Declaring does not run StartTurn effects.
            Assert.That(a.Deck.Hand.Single(), Is.SameAs(aCard));
            Assert.Throws<InvalidOperationException>(() => game.AnnounceRoundEnd());
            Assert.Throws<InvalidOperationException>(() => game.EndRound());
            var context = new ActionContext();
            Assert.That(game.BeginNextTurn(context), Is.SameAs(b));
            Assert.That(game.CurrentPlayer, Is.SameAs(b));
            game.EndCurrentTurn(context,new[]{bCard});
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
            Assert.Throws<InvalidOperationException>(() => game.BeginNextTurn(new ActionContext()));
            game.EndRound();
            Assert.That(game.Clock.DayPart, Is.EqualTo(DayPart.Night));
            Assert.Throws<InvalidOperationException>(() => game.EndRound());
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(Array.Empty<TacticCard>()));
            Assert.That(game.Clock.DayPart, Is.EqualTo(DayPart.Night));
            Assert.That(a.Deck.DrawPile.Single(), Is.SameAs(aCard));
            Assert.That(b.Deck.DrawPile.Single(), Is.SameAs(bCard));
            game.StartNewRound(Order(2, DayPart.Night));
            Assert.That(a.Deck.Hand.Single(), Is.SameAs(aCard));
            Assert.That(b.Deck.Hand.Single(), Is.SameAs(bCard));
        }

        [Test]
        public void NonEmptyDeckCannotDeclareAndRejectionDoesNotAdvanceOrder()
        {
            var a = new PlayerState(); var b = PlayerWithCard();
            a.Deck.SetDeck(Enumerable.Range(0, 6).Select(_ => new DeedCard("basic_card_000", CardType.Action)));
            var game = new GameEngine(new[] { a, b }, 3); game.StartNewRound(Order(2));
            Assert.That(a.Deck.DrawPileCount, Is.EqualTo(1));
            Assert.That(game.CanAnnounceRoundEnd, Is.False);
            Assert.Throws<InvalidOperationException>(() => game.AnnounceRoundEnd());
            Assert.That(game.ClockTracker.CountdownStarted, Is.False);
            Assert.That(game.BeginNextTurn(new ActionContext()), Is.SameAs(a));
        }

        [Test]
        public void EmptyPlayerMustDeclareRatherThanTakeAnEmptyActionTurn()
        {
            var a = new PlayerState(); var b = new PlayerState();
            var game = new GameEngine(new[] { a, b }, 3); game.StartNewRound(Order(2));
            Assert.Throws<InvalidOperationException>(() => game.BeginNextTurn(new ActionContext()));
            Assert.That(game.HasActiveTurn, Is.False);
            game.RunOneTurn(); // The convenience loop performs the required declaration.
            Assert.That(game.ClockTracker.CountdownStarted, Is.True);
            Assert.That(game.ClockTracker.RoundEnded, Is.False);
            game.RunOneTurn();
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
        }

        [Test]
        public void OnePlayerDeclarationEndsRoundWithoutGrantingAnotherTurn()
        {
            var p = PlayerWithCard(); var game = new GameEngine(new[] { p }, 3); game.StartNewRound(Order(1));
            game.AnnounceRoundEnd();
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
            Assert.That(game.HasActiveTurn, Is.False);
            Assert.Throws<InvalidOperationException>(() => game.BeginNextTurn(new ActionContext()));
            game.EndRound();
            Assert.That(game.Clock.DayPart, Is.EqualTo(DayPart.Night));
        }

        [Test]
        public void ActiveOrUnfinishedRoundCannotRefreshPlayerUnitsOrCards()
        {
            var a = PlayerWithCard(); var b = PlayerWithCard();
            var unit = new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"), "army_2_006"));
            a.Units.Add(unit);
            var game = new GameEngine(new[] { a, b }, 3); game.StartNewRound(Order(2));
            var context = new ActionContext(); game.BeginNextTurn(context); unit.Exhaust();
            Assert.Throws<InvalidOperationException>(() => game.AnnounceRoundEnd());
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(Order(2)));
            Assert.Throws<InvalidOperationException>(() => game.EndRound());
            Assert.That(unit.IsReady, Is.False); Assert.That(game.CurrentPlayer, Is.SameAs(a));
            game.EndCurrentTurn(context,new[]{a.Deck.Hand[0]});
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(Order(2)));
            Assert.Throws<InvalidOperationException>(() => game.EndRound());
            Assert.That(unit.IsReady, Is.False); Assert.That(game.Clock.DayPart, Is.EqualTo(DayPart.Day));
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("night")]
        [TestCase("number")]
        public void InvalidInitiativeSelectionRejectsBeforeRoundMutation(string reason)
        {
            var a = PlayerWithCard(); var b = PlayerWithCard();
            var unit = new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"), "army_2_006"));
            a.Units.Add(unit); unit.Exhaust();
            var game = new GameEngine(new[] { a, b }, 3);
            var picks = reason == "missing" ? Order(1) : Order(2);
            if (reason == "duplicate") picks[1] = picks[0];
            if (reason == "night") picks = Order(2, DayPart.Night);
            if (reason == "number") picks[0] = picks[0] with { Number = 7 };
            Assert.Throws<InvalidOperationException>(() => game.StartNewRound(picks));
            Assert.That(unit.IsReady, Is.False);
            Assert.That(a.Deck.Hand.Count, Is.EqualTo(1));
            Assert.That(game.HasActiveTurn, Is.False);
            game.StartNewRound(Order(2)); // Rejection does not leave half-started round state.
            Assert.That(unit.IsReady, Is.True);
        }

        [Test]
        public void RefreshOnlyCompatibilityEntryDoesNotCreateAPlayableInitiativeOrder()
        {
            var game = new GameEngine(new[] { PlayerWithCard() }, 3);
            game.StartNewRound(Array.Empty<TacticCard>());
            Assert.That(game.CanAnnounceRoundEnd, Is.False);
            Assert.Throws<InvalidOperationException>(() => game.BeginNextTurn(new ActionContext()));
        }

        [Test]
        public void FinalTurnSignalCannotDisplaceAnotherPlayersFinalTurn()
        {
            var a = PlayerWithCard(); var b = PlayerWithCard(); var c = PlayerWithCard();
            var game = new GameEngine(new[] { a, b, c }, 3); game.StartNewRound(Order(3)); game.AnnounceRoundEnd();
            var context = new ActionContext(); Assert.That(game.BeginNextTurn(context), Is.SameAs(b));
            // Exercise the scheduler's extra-turn signal, not any card's casting preconditions.
            context.ExtraTurn = true; game.EndCurrentTurn(context,new[]{b.Deck.Hand[0]});
            Assert.That(context.ExtraTurn, Is.False);
            Assert.That(game.ClockTracker.RoundEnded, Is.False);
            var last = new ActionContext(); Assert.That(game.BeginNextTurn(last), Is.SameAs(c));
            game.EndCurrentTurn(last,new[]{c.Deck.Hand[0]});
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
        }
    }
}
