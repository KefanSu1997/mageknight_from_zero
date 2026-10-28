using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class CrystalMasteryBasicStateTests
    {
        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("advanced_")
                ? CardJsonLoader.LoadAdvancedActions() : CardJsonLoader.LoadBasicActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        private static ActionContext Journey()
        {
            var map = new MapState();
            for (int q = 0; q <= 4; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q,
                    new[] { TerrainType.Plains });
            return new ActionContext { DayPart = DayPart.Day, MovementMap = map };
        }

        private static void Walk(PlayerState player, ActionContext context, int from, int to)
        {
            int before = context.MovementPool;
            for (int q = from + 1; q <= to; q++)
            {
                Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0),
                    context.MovementMap, context), Is.True);
                Assert.That(context.MovementPool, Is.EqualTo(before - 2 * (q - from)));
            }
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(to, 0)));
        }

        private static void EndTurn(PlayerState player, ActionContext context) =>
            new PlayerTurnEngine().EndTurn(player, context.DayPart,
                new ManaSource(1, () => context.DayPart, new Random(21)), context);

        private static void UniqueOriginals(PlayerState player, params DeedCard[] originals)
        {
            var all = player.Deck.Hand.Concat(player.Deck.PlayedCards)
                .Concat(player.Deck.DrawPile).Concat(player.Deck.DiscardPile).ToList();
            foreach (var card in originals)
                Assert.That(all.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1), card.Id);
        }

        [Test]
        public void BaseExistingGreenBecomesTwoPaysTwoOriginalMarchesAndDoesNotRefund()
        {
            var player = new PlayerState(); var context = Journey();
            player.Mana.AddCrystal(ManaColor.Green);
            var mastery = Add(player, "advanced_card_021");
            var first = Add(player, "basic_card_000");
            var second = Add(player, "basic_card_000");
            Assert.That(first, Is.Not.SameAs(second));

            Play(player, context, mastery, option: (int)ManaColor.Green);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(2));
            Assert.That(context.ReturnSpentCrystals, Is.False);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(mastery));
            Play(player, context, first, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Play(player, context, second, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(8));
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(2));
            Walk(player, context, 0, 4);
            Assert.That(context.MovementPool, Is.Zero);
            EndTurn(player, context);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals, Is.Empty);
            Assert.That(context.ReturnSpentCrystals, Is.False);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { mastery, first, second }));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.Hand, Is.Empty);
            UniqueOriginals(player, mastery, first, second);
        }

        [Test]
        public void StrongRefundsEarlierGreenOwnBlueAndLaterRedThenExpiresBeforeNextMarch()
        {
            var player = new PlayerState(); var context = Journey();
            foreach (var color in new[] { ManaColor.Green, ManaColor.Blue, ManaColor.Red })
                player.Mana.AddCrystal(color);
            var first = Add(player, "basic_card_000");
            var mastery = Add(player, "advanced_card_021");
            var fury = Add(player, "basic_card_021");
            // This separate original stays in hand through the first turn; no discarded card is moved back.
            var nextMarch = Add(player, "basic_card_000");
            Assert.That(first, Is.Not.SameAs(nextMarch));
            Play(player, context, first, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Walk(player, context, 0, 2);
            Play(player, context, mastery, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(context.ReturnSpentCrystals, Is.True);

            var game = new GameState(); game.Players.Add(player);
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_013");
            Assert.That(enemy.Armor, Is.EqualTo(4));
            Assert.That(enemy.Attack, Is.EqualTo(4));
            Assert.That(enemy.Fame, Is.EqualTo(2));
            var enemies = new List<Monster> { enemy };
            context.Enemies = enemies;
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            turn.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), context);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
            Play(player, context, fury, true);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            var attack = turn.Attack(new[] { 0 }, context);
            Assert.That(attack.PrintedPower, Is.EqualTo(4));
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(4));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.MeleePool, Is.Zero);
            turn.FinishBattle(context);
            Assert.That(context.InBattle, Is.False);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.End));
            Assert.That(player.Deck.PlayedCards, Is.EqualTo(new[] { first, mastery, fury }));

            context.SkipDraw = true; // Refund and permission expiry must precede the early-return path too.
            EndTurn(player, context);
            foreach (var color in new[] { ManaColor.Green, ManaColor.Blue, ManaColor.Red })
                Assert.That(player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.EqualTo(3));
            Assert.That(context.SpentCrystals, Is.Empty);
            Assert.That(context.ReturnSpentCrystals, Is.False);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { first, mastery, fury }));
            Assert.That(player.Deck.Hand.Single(c => c.Type != CardType.Wound), Is.SameAs(nextMarch));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(3));
            UniqueOriginals(player, first, mastery, fury, nextMarch);

            new PlayerTurnEngine().StartTurn(player, context.DayPart);
            Assert.That(context.TurnActionCommitted, Is.False);
            Play(player, context, nextMarch, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Walk(player, context, 2, 4);
            EndTurn(player, context);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.EqualTo(2));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals, Is.Empty);
            Assert.That(context.ReturnSpentCrystals, Is.False);
            Assert.That(player.Deck.DiscardPile, Is.EquivalentTo(new[] { first, mastery, fury, nextMarch }));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Wounds, Is.EqualTo(2));
            UniqueOriginals(player, first, mastery, fury, nextMarch);
        }

        [Test]
        public void ActuallySpentLastGreenLeavesZeroKeyAndBaseChoiceRejectsWithoutMovingCard()
        {
            var player = new PlayerState(); var context = Journey();
            player.Mana.AddCrystal(ManaColor.Green);
            var march = Add(player, "basic_card_000");
            var mastery = Add(player, "advanced_card_021");
            Play(player, context, march, true);
            Walk(player, context, 0, 2);
            Assert.That(player.Mana.Crystals.ContainsKey(ManaColor.Green), Is.True);
            Assert.That(player.Mana.Crystals[ManaColor.Green], Is.Zero);
            Assert.Throws<InvalidOperationException>(() =>
                Play(player, context, mastery, option: (int)ManaColor.Green));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.ReturnSpentCrystals, Is.False);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(mastery));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(march));
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Wounds, Is.Zero);
            UniqueOriginals(player, march, mastery);
        }
    }
}
