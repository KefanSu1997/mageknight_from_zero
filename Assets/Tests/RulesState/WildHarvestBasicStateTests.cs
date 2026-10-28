using System;
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
    public sealed class WildHarvestBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static ActionContext Route()
        {
            var map = new MapState();
            for (int q = 0; q <= 3; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return new ActionContext { DayPart = DayPart.Day, MovementMap = map };
        }

        private static DeedCard Add(PlayerState player, string id, CardType type = CardType.Action)
        {
            var card = new DeedCard(id, type);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong = false)
            => CardPlayActions.Play(player, context, card,
                CardJsonLoader.LoadBasicActions().Single(c => c.Id == card.Id), strong);

        private static bool Move(PlayerState player, ActionContext context, int q,
            DeedCard material = null, ManaColor? color = null)
            => new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0), context.MovementMap,
                context, harvestCard: material, harvestColor: color);

        private static void InDiscard(PlayerState player, params DeedCard[] cards)
        {
            foreach (var card in cards)
            {
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
                Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
                Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card)), Is.False);
            }
        }

        private static void End(PlayerState player, ActionContext context)
        {
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(context.HarvestTriggers, Is.Zero);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
        }

        [Test]
        public void BaseHarvestsOneRealGreenCardAndItsCrystalPaysOriginalMarch()
        {
            var player = new PlayerState();
            var context = Route();
            var harvest = Add(player, "basic_card_003");
            var tranquility = Add(player, "basic_card_001");
            var march = Add(player, "basic_card_000");
            var fury = Add(player, "basic_card_021");
            Assert.That(CardColorHelper.Get(tranquility.Id), Is.EqualTo(ManaColor.Green));
            Play(player, context, harvest);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(context.HarvestTriggers, Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(harvest));
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Mana.Crystals, Is.Empty);

            Assert.That(Move(player, context, 1, tranquility), Is.True);
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.HarvestTriggers, Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            InDiscard(player, tranquility);
            Play(player, context, march, true);
            Assert.That(player.Mana.Tokens, Is.Empty);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(4));

            Assert.Throws<InvalidOperationException>(() => Move(player, context, 2, fury));
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(fury));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(tranquility));
            Assert.That(Move(player, context, 2), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(Move(player, context, 3), Is.True);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(harvest));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(march));
            End(player, context);
            InDiscard(player, harvest, tranquility, march);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(3));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(fury));
        }

        [Test]
        public void StrongHarvestsEachStepAndDiscardsOriginalSwordForChosenCrystal()
        {
            var player = new PlayerState();
            var context = Route();
            var harvest = Add(player, "basic_card_003");
            var tranquility = Add(player, "basic_card_001");
            var sword = Add(player, "items_008", CardType.Artifact);
            var march = Add(player, "basic_card_000");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Green);
            Play(player, context, harvest, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(context.HarvestTriggers, Is.EqualTo(-1));

            Assert.That(Move(player, context, 1, tranquility), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Play(player, context, march, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(6));
            Assert.That(Move(player, context, 2, sword, ManaColor.Red), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(context.HarvestTriggers, Is.EqualTo(-1));
            InDiscard(player, tranquility, sword);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(2));
            Assert.That(context.MeleePool, Is.Zero, "Discarding the sword as material must not play its effect");
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(fury));

            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_013");
            context.Enemies = new List<MK.Logic.Data.Monster> { enemy };
            var game = new GameState(); game.Players.Add(player);
            var machine = new TurnMachine(game);
            machine.BeginBattle(context);
            machine.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            machine.SetBlock(0, Element.Physical);
            var defense = machine.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(defense.TotalWounds, Is.EqualTo(2));
            Play(player, context, fury, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Tokens, Is.Empty);
            var attack = machine.Attack(new[] { 0 }, context);
            Assert.That(attack.PrintedPower, Is.EqualTo(4));
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(4));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(harvest));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(march));
            Assert.That(player.Deck.PlayedCards[2], Is.SameAs(fury));
            machine.FinishBattle(context);
            End(player, context);
            InDiscard(player, harvest, tranquility, sword, march, fury);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(5));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
        }

        [Test]
        public void FailedOrRepeatedMoveDoesNotHarvestAndStrongPermissionEndsWithTurn()
        {
            var player = new PlayerState();
            var context = Route();
            var harvest = Add(player, "basic_card_003");
            var tranquility = Add(player, "basic_card_001");
            var march = Add(player, "basic_card_000");
            player.Mana.AddToken(ManaColor.Green);
            Play(player, context, harvest, true);
            Assert.That(Move(player, context, 1), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(context.HarvestTriggers, Is.EqualTo(-1));
            Assert.That(Move(player, context, 1, tranquility), Is.False);
            Assert.That(Move(player, context, 3, tranquility), Is.False);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(player.Deck.Hand[0], Is.SameAs(tranquility));
            Assert.That(player.Deck.Hand[1], Is.SameAs(march));
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Mana.Crystals, Is.Empty);
            End(player, context);
            new PlayerTurnEngine().StartTurn(player, DayPart.Day);
            Play(player, context, march);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => Move(player, context, 2, tranquility));
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1, 0)));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(tranquility));
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(Move(player, context, 2), Is.True);
            Assert.That(context.MovementPool, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForeignInstanceOrColorlessWoundRejectsBeforeMovementPayment(bool wound)
        {
            var player = new PlayerState();
            var context = Route();
            var harvest = Add(player, "basic_card_003");
            var heldGreen = Add(player, "basic_card_001");
            DeedCard invalid;
            if (wound)
            {
                player.Deck.GainWound();
                invalid = player.Deck.Hand.Last();
            }
            else invalid = new DeedCard(heldGreen.Id, heldGreen.Type);
            var hand = player.Deck.Hand.Where(c => !ReferenceEquals(c, harvest)).ToArray();
            player.Mana.AddToken(ManaColor.Green);
            Play(player, context, harvest, true);
            Assert.Throws<InvalidOperationException>(() => Move(player, context, 1, invalid));
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(context.HarvestTriggers, Is.EqualTo(-1));
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(player.Mana.Tokens, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(harvest));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(hand.Length));
            for (int i = 0; i < hand.Length; i++) Assert.That(player.Deck.Hand[i], Is.SameAs(hand[i]));
        }
    }
}
