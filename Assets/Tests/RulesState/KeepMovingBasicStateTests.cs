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
    public sealed class KeepMovingBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static ActionContext Route()
        {
            var context = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState() };
            var terrain = new[] { TerrainType.Plains, TerrainType.Forest, TerrainType.Hills, TerrainType.Forest };
            for (int q = 0; q < terrain.Length; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { terrain[q] });
            return context;
        }

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong = false)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong);
        }

        private static void Move(PlayerState player, ActionContext context, int q, int remaining)
        {
            var destination = new AxialCoord(q, 0);
            Assert.That(MovementService.GetCost(destination, context.MovementMap, DayPart.Day, context), Is.EqualTo(3));
            Assert.That(new MovementService().TryMoveUsingPool(player, destination, context.MovementMap, context), Is.True);
            Assert.That(player.Position, Is.EqualTo(destination));
            Assert.That(context.MovementPool, Is.EqualTo(remaining));
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
        public void OriginalMovementThenSidewaysDistinguishesNextFromAllCards(bool strong)
        {
            var player = new PlayerState();
            var context = Route();
            var keepMoving = Add(player, "basic_card_017");
            var promise = Add(player, "basic_card_008");
            var agility = Add(player, "advanced_card_006");
            var sidewaysFury = Add(player, "basic_card_021");
            if (strong) player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, keepMoving, strong);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Play(player, context, promise);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(context.MoveBonusNext, Is.EqualTo(strong ? 0 : 1));

            // Agility supplies movement through its own original effect, not MoveEffect.
            Play(player, context, agility);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 7 : 5));
            Assert.That(context.MoveBonusNext, Is.Zero);
            Assert.That(new ActionSystem(false).PlaySideways(sidewaysFury, player, context, SidewaysType.Move), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 9 : 6));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            PlayedExactly(player, keepMoving, promise, agility, sidewaysFury);

            Move(player, context, 1, strong ? 6 : 3);
            Move(player, context, 2, strong ? 3 : 0);
            if (strong) Move(player, context, 3, 0);
            Assert.That(context.MovementPool, Is.Zero);
        }

        [Test]
        public void BaseSidewaysFirstConsumesNextBonusBeforeOriginalMarch()
        {
            var player = new PlayerState();
            var context = Route();
            var keepMoving = Add(player, "basic_card_017");
            var sidewaysFury = Add(player, "basic_card_021");
            var march = Add(player, "basic_card_000");

            Play(player, context, keepMoving);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(new ActionSystem(false).PlaySideways(sidewaysFury, player, context, SidewaysType.Move), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(context.MoveBonusNext, Is.Zero);
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(sidewaysFury));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(march));
            Assert.That(player.Deck.DiscardPile, Is.Empty);

            Play(player, context, march);
            Assert.That(context.MovementPool, Is.EqualTo(6));
            Assert.That(context.MoveBonusNext, Is.Zero);
            PlayedExactly(player, keepMoving, sidewaysFury, march);
            Move(player, context, 1, 3);
            Move(player, context, 2, 0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnusedMovementPermissionExpiresBeforeNextTurnOriginalAndSidewaysCards(bool strong)
        {
            var player = new PlayerState();
            var context = Route();
            var keepMoving = Add(player, "basic_card_017");
            var march = Add(player, "basic_card_000");
            var sidewaysFury = Add(player, "basic_card_021");
            if (strong) player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, keepMoving, strong);
            Assert.That(context.MoveBonusNext, Is.EqualTo(strong ? 0 : 1));
            Assert.That(context.MoveBonusAll, Is.EqualTo(strong ? 1 : 0));
            var turns = new PlayerTurnEngine();
            turns.EndTurn(player, DayPart.Day, null, context);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.MoveBonusNext, Is.Zero);
            Assert.That(context.MoveBonusAll, Is.Zero);
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(keepMoving));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));

            turns.StartTurn(player, DayPart.Day);
            Play(player, context, march);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(new ActionSystem(false).PlaySideways(sidewaysFury, player, context, SidewaysType.Move), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(3));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(march));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(sidewaysFury));
            Assert.That(player.Deck.Hand, Is.Empty);
            Move(player, context, 1, 0);
        }
    }
}
