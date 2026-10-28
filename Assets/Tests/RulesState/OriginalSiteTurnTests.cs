using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Map
{
    public sealed class OriginalSiteTurnTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (GameEngine game, PlayerState player, ActionContext context) Start(PlaceType type, bool night = false)
        {
            var map = new MapState();
            var supply = new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Where(m=>m.TokenColor==EnemyTokenColor.Green));
            OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map, supply);
            var p = new PlayerState { Position = map.Places.First(x => x.Value.Type == type).Key };
            p.Deck.SetDeck(Enumerable.Range(0, 12).Select(_ => new DeedCard("basic_card_000", CardType.Action)));
            var g = new GameEngine(new[] { p }, 3, map: map, manaRandom: new Random(0));
            if (night) g.Clock.NextRound();
            g.StartNewRound(new[] { OriginalTactics.Get(night ? "night_1" : "day_1").Card });
            var c = new ActionContext(); g.BeginNextTurn(c); return (g, p, c);
        }
        private static void PlayMarch(PlayerState p, ActionContext c)
        { var card = p.Deck.Hand.First(x => x.Id == "basic_card_000"); CardPlayActions.Play(p, c, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id)); }

        [TestCase(false)] [TestCase(true)]
        public void GladeStartUsesActualDayNightTemporaryTokenOnceAndExpires(bool night)
        {
            var (g, p, c) = Start(PlaceType.MagicalGlade, night); var color = night ? ManaColor.Black : ManaColor.Gold;
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1)); Assert.That(p.Mana.Crystals, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => g.BeginNextTurn(new ActionContext())); Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            PlayMarch(p, c); g.EndCurrentTurn(c); Assert.That(p.Mana.Tokens, Is.Empty);
            var next = new ActionContext(); g.BeginNextTurn(next); Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void GladeExplicitHandOrDiscardRemovalPreservesOtherCopiesAndIsNotHealing(bool discard)
        {
            var (g, p, c) = Start(PlaceType.MagicalGlade); p.Deck.GainWound(discard); p.Deck.GainWound(discard);
            var wounds = (discard ? p.Deck.DiscardPile : p.Deck.Hand).Where(x => x.Type == CardType.Wound).ToArray();
            var chosen = wounds[0]; var retained = wounds[1]; PlayMarch(p, c);
            c.DrawPerHeal = 3; c.Healing.Add(2); int drawBefore = p.Deck.DrawPileCount;
            g.BeginEndCurrentTurn(c, siteSelection: new SiteTurnSelection { GladeWound = chosen });
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, chosen)) || p.Deck.DiscardPile.Any(x => ReferenceEquals(x, chosen)), Is.False);
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, retained)) || p.Deck.DiscardPile.Any(x => ReferenceEquals(x, retained)), Is.True);
            Assert.That(p.Deck.RemovedCards, Is.Empty); Assert.That(c.HealedHandWoundsThisTurn, Is.Zero);
            Assert.That(p.Deck.DrawPileCount, Is.EqualTo(drawBefore));
            Assert.Throws<InvalidOperationException>(() => g.BeginEndCurrentTurn(c, siteSelection: new SiteTurnSelection { GladeWound = retained }));
            g.CompleteEndCurrentTurn(c);
        }

        [Test] public void GladeDefaultDoesNotRemoveAndRestStillAllowsExactDiscardWoundChoice()
        {
            var (g, p, c) = Start(PlaceType.MagicalGlade); p.Deck.GainWound(); var wound = p.Deck.Hand.Last();
            RestActions.Rest(p, c, p.Deck.Hand.First(), new[] { wound });
            g.BeginEndCurrentTurn(c); Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x, wound)), Is.True);
            g.CompleteEndCurrentTurn(c); var next = new ActionContext(); g.BeginNextTurn(next);
            RestActions.Rest(p, next, p.Deck.Hand.First(), Array.Empty<DeedCard>());
            g.EndCurrentTurn(next, siteSelection: new SiteTurnSelection { GladeWound = wound });
            Assert.That(p.Deck.DiscardPile.Any(x => ReferenceEquals(x, wound)), Is.False); Assert.That(p.Deck.RemovedCards, Is.Empty);
        }

        [TestCase(0, 1)] [TestCase(3, 3)]
        public void ActualGreenMineAwardsStockOnceWithNoOverflowToken(int before, int expected)
        {
            var (g, p, c) = Start(PlaceType.ManaMine); p.Mana.AddCrystal(ManaColor.Green, before); PlayMarch(p, c);
            g.BeginEndCurrentTurn(c); Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(expected)); Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => g.BeginEndCurrentTurn(c)); Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(expected));
            g.CompleteEndCurrentTurn(c); Assert.Throws<InvalidOperationException>(() => g.EndCurrentTurn(c)); Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(expected));
        }

        [TestCase(false)] [TestCase(true)]
        public void InvalidSiteSelectionRejectsBeforeAnyTurnCleanup(bool mine)
        {
            var (g, p, c) = Start(mine ? PlaceType.ManaMine : PlaceType.MagicalGlade); PlayMarch(p, c); p.Mana.AddToken(ManaColor.Red);
            var played = p.Deck.PlayedCards.Single();
            var selection = mine ? new SiteTurnSelection { MineColor = ManaColor.Blue } : new SiteTurnSelection { GladeWound = new DeedCard("w", CardType.Wound) };
            Assert.Throws<InvalidOperationException>(() => g.BeginEndCurrentTurn(c, siteSelection: selection));
            Assert.That(g.IsEndingTurn, Is.False); Assert.That(g.HasActiveTurn, Is.True); Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(played));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Crystals, Is.Empty);
        }

        [Test] public void ActualMovementChangesEndSiteAndDoesNotGrantAnotherStartToken()
        {
            var (g, p, c) = Start(PlaceType.MagicalGlade); p.Deck.GainWound(); var wound = p.Deck.Hand.Last(); PlayMarch(p, c);
            var village = p.Position + new AxialCoord(1, 0);
            Assert.That(g.Map.Places[village].Type, Is.EqualTo(PlaceType.Village));
            Assert.That(new MovementService().TryMoveUsingPool(p, village, g.Map, c), Is.True);
            Assert.That(p.Mana.Tokens[ManaColor.Gold], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => g.BeginEndCurrentTurn(c, siteSelection: new SiteTurnSelection { GladeWound = wound }));
            g.EndCurrentTurn(c); Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, wound)), Is.True);
            g.BeginNextTurn(new ActionContext()); Assert.That(p.Mana.Tokens, Is.Empty);
        }
    }
}
