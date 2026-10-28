using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SteadyTempoBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState player, ActionContext context, DeedCard tempo, DeedCard[] draw)
            Start(bool strong, bool emptyDeck = false)
        {
            var player = new PlayerState();
            var context = new ActionContext
            {
                DayPart = DayPart.Day,
                CurrentTerrain = TerrainType.Plains,
                MovementMap = new MapState()
            };
            for (int q = 0; q <= 2; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });

            var draw = emptyDeck ? System.Array.Empty<DeedCard>() : new[]
            {
                new DeedCard("basic_card_000", CardType.Action),
                new DeedCard("basic_card_008", CardType.Action)
            };
            // Preserve the actual March / Promise draw order; do not synthesize a copy when recycling.
            foreach (var card in draw)
                Assert.That(CardJsonLoader.LoadBasicActions().Any(original => original.Id == card.Id), Is.True);
            player.Deck.SetDeck(draw);
            var tempo = new DeedCard("advanced_card_017", CardType.Action);
            player.Deck.Hand.Add(tempo);
            if (strong) player.Mana.AddToken(ManaColor.Blue);
            return (player, context, tempo, draw);
        }

        private static void PlayAndConsumeMovement(PlayerState player, ActionContext context,
            DeedCard tempo, bool strong)
        {
            var original = CardJsonLoader.LoadAdvancedActions().Single(card => card.Id == tempo.Id);
            Assert.That(original.Name, Is.EqualTo("稳步前进"));
            Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Blue }));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(strong ? 1 : 0));
            CardPlayActions.Play(player, context, tempo, original, strong);

            int movement = strong ? 4 : 2;
            Assert.That(context.MovementPool, Is.EqualTo(movement));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(tempo));
            Assert.That(player.Deck.DiscardPile, Is.Empty);

            for (int q = 1; q <= movement / 2; q++)
            {
                var destination = new AxialCoord(q, 0);
                Assert.That(MovementService.GetCost(destination, context.MovementMap, DayPart.Day, context),
                    Is.EqualTo(2));
                Assert.That(new MovementService().TryMoveUsingPool(player, destination, context.MovementMap, context),
                    Is.True);
                Assert.That(player.Position, Is.EqualTo(destination));
                Assert.That(context.MovementPool, Is.EqualTo(movement - 2 * q));
            }
            Assert.That(context.MovementPool, Is.Zero);
        }

        private static void Exact(IReadOnlyList<DeedCard> actual, params DeedCard[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(actual[i], Is.SameAs(expected[i]));
        }

        private static void SelectAndEnd(PlayerState player, ActionContext context, DeedCard tempo,
            DeedCard[] draw, bool recycle)
        {
            CardPlayActions.SelectSteadyTempoReturn(player, context, tempo, recycle);
            // Selection is deferred: the played card must not enter the draw pile before EndTurn.
            Exact(player.Deck.DrawPile, draw);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(tempo));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);

            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DrawPile, Is.Empty);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(context.MovementPool, Is.Zero);
            var all = player.Deck.Hand.Concat(player.Deck.PlayedCards).Concat(player.Deck.DrawPile)
                .Concat(player.Deck.DiscardPile).Concat(player.Deck.RemovedCards).ToArray();
            Assert.That(all.Length, Is.EqualTo(draw.Length + 1));
            foreach (var original in draw.Append(tempo))
                Assert.That(all.Count(card => ReferenceEquals(card, original)), Is.EqualTo(1),
                    $"Original instance {original.Id} must occupy exactly one card zone.");
        }

        [Test]
        public void BaseReturnsActualPlayedCardToNonemptyDeckBottomBeforeRefill()
        {
            var (player, context, tempo, draw) = Start(false);
            PlayAndConsumeMovement(player, context, tempo, false);
            SelectAndEnd(player, context, tempo, draw, true);
            Exact(player.Deck.Hand, draw[0], draw[1], tempo);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void StrongPaysBlueAndReturnsActualPlayedCardToDeckTopBeforeRefill()
        {
            var (player, context, tempo, draw) = Start(true);
            PlayAndConsumeMovement(player, context, tempo, true);
            SelectAndEnd(player, context, tempo, draw, true);
            Exact(player.Deck.Hand, tempo, draw[0], draw[1]);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void StrongMayReturnActualPlayedCardEvenWhenDeckIsEmpty()
        {
            var (player, context, tempo, draw) = Start(true, true);
            PlayAndConsumeMovement(player, context, tempo, true);
            SelectAndEnd(player, context, tempo, draw, true);
            Exact(player.Deck.Hand, tempo);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void StrongCanDeclineReturnAndDiscardOnlyItsActualPlayedInstance()
        {
            var (player, context, tempo, draw) = Start(true);
            PlayAndConsumeMovement(player, context, tempo, true);
            SelectAndEnd(player, context, tempo, draw, false);
            Exact(player.Deck.Hand, draw);
            Exact(player.Deck.DiscardPile, tempo);
        }

        [Test]
        public void BaseReturnChoiceOnEmptyDeckFallsBackToNormalDiscard()
        {
            var (player, context, tempo, draw) = Start(false, true);
            PlayAndConsumeMovement(player, context, tempo, false);
            SelectAndEnd(player, context, tempo, draw, true);
            Assert.That(player.Deck.Hand, Is.Empty);
            Exact(player.Deck.DiscardPile, tempo);
        }
    }
}
