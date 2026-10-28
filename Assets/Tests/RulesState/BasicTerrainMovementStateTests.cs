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
    public sealed class BasicTerrainMovementStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static ActionContext Route(DayPart time, params TerrainType[] destinations)
        {
            var context = new ActionContext
            {
                MovementMap = new MapState(), DayPart = time, CurrentTerrain = TerrainType.Plains
            };
            context.MovementMap.Placed[new AxialCoord(0, 0)] =
                new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            for (int i = 0; i < destinations.Length; i++)
                context.MovementMap.Placed[new AxialCoord(i + 1, 0)] =
                    new MapTile(TileSet.Countryside, i + 1, new[] { destinations[i] });
            return context;
        }

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong);
        }

        private static void Move(PlayerState player, ActionContext context, int q, int expectedCost, int remaining)
        {
            var destination = new AxialCoord(q, 0);
            Assert.That(MovementService.GetCost(destination, context.MovementMap, context.DayPart, context),
                Is.EqualTo(expectedCost));
            Assert.That(new MovementService().TryMoveUsingPool(player, destination, context.MovementMap, context), Is.True);
            Assert.That(player.Position, Is.EqualTo(destination));
            Assert.That(context.MovementPool, Is.EqualTo(remaining));
            Assert.That(context.CurrentTerrain, Is.EqualTo(context.MovementMap.Placed[destination].Edges[0]));
        }

        private static void PlayedExactly(PlayerState player, params DeedCard[] cards)
        {
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(cards.Length));
            for (int i = 0; i < cards.Length; i++)
                Assert.That(player.Deck.PlayedCards[i], Is.SameAs(cards[i]));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Wounds, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FrostBridgeMovesAcrossPrintedTerrain(bool strong)
        {
            var player = new PlayerState();
            var context = strong
                ? Route(DayPart.Day, TerrainType.Lake, TerrainType.Swamp)
                : Route(DayPart.Day, TerrainType.Swamp);
            var bridge = Add(player, "advanced_card_009");
            if (strong) player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, bridge, strong);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Move(player, context, 1, 1, strong ? 3 : 1);
            if (strong) Move(player, context, 2, 1, 2);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            PlayedExactly(player, bridge);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WindSongPaysWhiteThenBlueAndMovesAcrossItsTerrains(bool strong)
        {
            var player = new PlayerState();
            var context = Route(DayPart.Night,
                TerrainType.Plains, TerrainType.Desert, TerrainType.Wasteland, TerrainType.Lake);
            var song = Add(player, "advanced_card_010");
            var march = Add(player, "basic_card_000");
            player.Mana.AddToken(ManaColor.Green);
            if (strong)
            {
                // The original white printed symbol and the later blue lake fee are separate payments.
                player.Mana.AddToken(ManaColor.White);
                player.Mana.AddToken(ManaColor.Blue);
            }

            Play(player, context, song, strong);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(strong ? 1 : 0));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(song));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(march));

            // Printed March4 supplies the extra movement; it does not modify any terrain cost.
            Play(player, context, march, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(6));
            Move(player, context, 1, strong ? 0 : 1, strong ? 6 : 5);
            Move(player, context, 2, strong ? 1 : 2, strong ? 5 : 3);
            Move(player, context, 3, strong ? 2 : 3, strong ? 3 : 0);
            if (strong)
            {
                Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
                Move(player, context, 4, 0, 3);
                Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            }
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            PlayedExactly(player, song, march);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PathFindingMovesAtPrintedFloorAndEnhancedCost(bool strong)
        {
            var player = new PlayerState();
            var context = strong
                ? Route(DayPart.Night, TerrainType.Forest, TerrainType.Swamp)
                : Route(DayPart.Day, TerrainType.Forest, TerrainType.Plains);
            var path = Add(player, "advanced_card_011");
            var march = strong ? null : Add(player, "basic_card_000");
            if (strong) player.Mana.AddToken(ManaColor.Green);

            Play(player, context, path, strong);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            if (strong)
            {
                // Ordinary movement discounts do not grant permission to enter mountains.
                var mountain = new AxialCoord(0, 1);
                context.MovementMap.Placed[mountain] =
                    new MapTile(TileSet.Countryside, 3, new[] { TerrainType.Mountain });
                Assert.That(new MovementService().TryMoveUsingPool(player, mountain, context.MovementMap, context), Is.False);
                Assert.That(player.Position, Is.EqualTo(new AxialCoord(0, 0)));
                Assert.That(context.MovementPool, Is.EqualTo(4));
            }
            else
            {
                Play(player, context, march, false);
                Assert.That(context.MovementPool, Is.EqualTo(4));
            }

            Move(player, context, 1, 2, 2);
            Move(player, context, 2, 2, 0);
            if (strong) PlayedExactly(player, path);
            else PlayedExactly(player, path, march);
        }
    }
}
