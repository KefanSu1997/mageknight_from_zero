using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ManaPowerBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static ActionContext Route()
        {
            var context = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState() };
            for (int q = 0; q <= 2; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return context;
        }

        private static DeedCard Add(PlayerState player, string id = "advanced_card_040", CardType type = CardType.Action)
        {
            var card = new DeedCard(id, type); player.Deck.Hand.Add(card); return card;
        }

        private static void Inventory(PlayerState player, int each)
        {
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                player.Mana.AddCrystal(color, each);
        }

        private static void AssertInventory(PlayerState player, int each)
        {
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                Assert.That(player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(each));
        }

        private static void Choose(ActionContext context, ManaColor color, params int[] bonuses)
        {
            context.SelectedActionManaColor = color;
            context.ManaPowerBonusChoices.Clear();
            context.ManaPowerBonusChoices.AddRange(bonuses);
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong, int option = 0)
        {
            var original = (card.Id == "advanced_card_040" ? CardJsonLoader.LoadAdvancedActions()
                : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_040")
            {
                Assert.That(original.Name, Is.EqualTo("魔晶力量"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Green, ManaColor.Blue }));
            }
            else Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Green }));
            CardPlayActions.Play(player, context, card, original, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static void PaidAndCleared(PlayerState player, ActionContext context)
        {
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SelectedActionManaColor, Is.Null);
            Assert.That(context.ManaPowerBonusChoices, Is.Empty);
            Assert.That(context.MeleePool + context.BlockPool + context.RangedPool + context.InfluencePool, Is.Zero);
        }

        private static void Move(PlayerState player, ActionContext context, int q, int remaining)
        {
            var destination = new AxialCoord(q, 0);
            Assert.That(MovementService.GetCost(destination, context.MovementMap, DayPart.Day, context), Is.EqualTo(2));
            Assert.That(new MovementService().TryMoveUsingPool(player, destination, context.MovementMap, context), Is.True);
            Assert.That(player.Position, Is.EqualTo(destination));
            Assert.That(context.MovementPool, Is.EqualTo(remaining));
        }

        private static void Exact(IReadOnlyList<DeedCard> actual, params DeedCard[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(actual[i], Is.SameAs(expected[i]));
        }

        private static void End(PlayerState player, ActionContext context, params DeedCard[] played)
        {
            context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(played.Length));
            foreach (var card in played)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(context.MovementPool, Is.Zero);
        }

        [Test]
        public void BaseMissingGreenCrystalPaysOriginalMarchAndRealMovement()
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 1);
            player.Mana.Crystals[ManaColor.Green] = 0;
            var source = Add(player); var march = Add(player, "basic_card_000");
            Play(player, context, source, false, (int)ManaColor.Green);
            AssertInventory(player, 1);
            Assert.That(context.MovementPool, Is.Zero);
            Play(player, context, march, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White })
                Assert.That(player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Move(player, context, 1, 2); Move(player, context, 2, 0);
            End(player, context, source, march);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand, Is.Empty);
        }

        [Test]
        public void GreenPaidMoveChoosesIndependentHealAndDrawBonusesFromTwoSets()
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 2);
            player.Mana.AddToken(ManaColor.Green);
            var march = new DeedCard("basic_card_000", CardType.Action);
            var promise = new DeedCard("basic_card_008", CardType.Action);
            player.Deck.SetDeck(new[] { march, promise });
            var source = Add(player); var wound = Add(player, "w", CardType.Wound);
            Choose(context, ManaColor.Green, 1, 2);
            Play(player, context, source, true, 0);
            PaidAndCleared(player, context); AssertInventory(player, 2);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(context.Healing.Total, Is.EqualTo(1));
            Exact(player.Deck.Hand, wound, march); Exact(player.Deck.DrawPile, promise);
            CardHealing.Spend(player, context, 1);
            Assert.That(player.Wounds, Is.Zero); Assert.That(context.Healing.Total, Is.Zero);
            Exact(player.Deck.Hand, march);
            Move(player, context, 1, 2); Move(player, context, 2, 0);
            End(player, context, source); AssertInventory(player, 2);
            Exact(player.Deck.Hand, march); Exact(player.Deck.DrawPile, promise);
        }

        [Test]
        public void BluePaidHealingTreatsOriginalFarmerAndHandThenUnitActuallyMoves()
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 1);
            player.Mana.AddToken(ManaColor.Blue);
            var source = Add(player); var wound1 = Add(player, "w", CardType.Wound);
            var wound2 = Add(player, "w", CardType.Wound);
            var farmer = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            Assert.That(farmer.Card.Level, Is.EqualTo(1));
            farmer.AddWounds(1); player.Units.Add(farmer);
            Assert.That(farmer.IsReady, Is.True); Assert.That(farmer.CanActivate, Is.False);
            Choose(context, ManaColor.Blue, 1);
            Play(player, context, source, true, 1);
            PaidAndCleared(player, context); AssertInventory(player, 1);
            Assert.That(context.Healing.Total, Is.EqualTo(3));
            Assert.That(context.MovementPool, Is.Zero);
            Exact(player.Deck.Hand, wound1, wound2);
            CardHealing.Spend(player, context, 1, farmer);
            Assert.That(farmer.Wounds, Is.Zero); Assert.That(farmer.CanActivate, Is.True);
            Assert.That(context.Healing.Total, Is.EqualTo(2));
            CardHealing.Spend(player, context, 2);
            Assert.That(player.Wounds, Is.Zero); Assert.That(context.Healing.Total, Is.Zero);
            CardUnitActions.Use(player, context, farmer, 2);
            Assert.That(farmer.IsReady, Is.False); Assert.That(context.MovementPool, Is.EqualTo(2));
            Move(player, context, 1, 0);
            End(player, context, source); AssertInventory(player, 1);
            Assert.That(player.Units.Single(), Is.SameAs(farmer));
            Assert.That(player.Deck.Hand, Is.Empty);
        }

        [Test]
        public void DrawMainAndMoveBonusUseExactDrawOrderAndDrawnOriginalMarch()
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 1);
            player.Mana.AddToken(ManaColor.Green);
            var march = new DeedCard("basic_card_000", CardType.Action);
            var promise = new DeedCard("basic_card_008", CardType.Action);
            var fury = new DeedCard("basic_card_021", CardType.Action);
            player.Deck.SetDeck(new[] { march, promise, fury });
            var source = Add(player);
            Choose(context, ManaColor.Green, 0);
            Play(player, context, source, true, 2);
            PaidAndCleared(player, context); AssertInventory(player, 1);
            Assert.That(context.MovementPool, Is.EqualTo(2)); Assert.That(context.Healing.Total, Is.Zero);
            Exact(player.Deck.Hand, march, promise); Exact(player.Deck.DrawPile, fury);
            Move(player, context, 1, 0);
            Play(player, context, march, false);
            Assert.That(context.MovementPool, Is.EqualTo(2)); Move(player, context, 2, 0);
            End(player, context, source, march); AssertInventory(player, 1);
            Exact(player.Deck.Hand, promise); Exact(player.Deck.DrawPile, fury);
        }

        [Test]
        public void SpendingOnlyBlueCrystalBreaksItsSetBeforeBonusCount()
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 1);
            var source = Add(player); Choose(context, ManaColor.Blue);
            Play(player, context, source, true, 0);
            PaidAndCleared(player, context);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            foreach (var color in new[] { ManaColor.Red, ManaColor.Green, ManaColor.White })
                Assert.That(player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4)); Assert.That(context.Healing.Total, Is.Zero);
            Move(player, context, 1, 2); Move(player, context, 2, 0); End(player, context, source);
        }

        [TestCase(0)] // Base chooses an already-owned color.
        [TestCase(1)] // Strong invalid main choice.
        [TestCase(2)] // Strong one complete set but no corresponding choice.
        [TestCase(3)] // Healing is forbidden in battle, before payment.
        public void InvalidSelectionPreservesRealSourceAndAllPayment(int scenario)
        {
            var player = new PlayerState(); var context = Route(); Inventory(player, 1);
            var source = Add(player); player.Mana.AddToken(ManaColor.Blue);
            Choose(context, ManaColor.Blue);
            if (scenario == 1) context.ManaPowerBonusChoices.Add(0);
            if (scenario == 3) { context.InBattle = true; context.ManaPowerBonusChoices.Add(1); }
            var original = CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == source.Id);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.Play(player, context, source,
                original, scenario != 0, scenario == 1 ? 3 : 0));
            Exact(player.Deck.Hand, source); Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty); Assert.That(player.Deck.RemovedCards, Is.Empty);
            AssertInventory(player, 1); Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(context.MovementPool + context.Healing.Total, Is.Zero);
        }
    }
}
