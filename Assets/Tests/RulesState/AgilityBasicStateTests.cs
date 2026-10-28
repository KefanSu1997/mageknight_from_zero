using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class AgilityBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

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
            CardPlayActions.Play(player, context, card, original, strong, 0);
        }

        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);

        private static void AssertAttack(CardCombatActions.Result attack, int targetArmor)
        {
            Assert.That(attack.PrintedPower, Is.EqualTo(4));
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(targetArmor));
            Assert.That(attack.Kills, Is.EqualTo(1));
        }

        private static void PlayedExactly(PlayerState player, params DeedCard[] cards)
        {
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(cards.Length));
            for (int i = 0; i < cards.Length; i++)
                Assert.That(player.Deck.PlayedCards[i], Is.SameAs(cards[i]));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OriginalMovementIsSpentOnMapAndConversionRequiresCombat(bool strong)
        {
            var player = new PlayerState();
            var context = new ActionContext { MovementMap = new MapState(), DayPart = DayPart.Day };
            for (int q = 0; q <= 2; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            var agility = Add(player, "advanced_card_006");
            if (strong) player.Mana.AddToken(ManaColor.White);
            Play(player, context, agility, strong);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));

            var actions = new ActionSystem(false);
            // Both printed effects limit their conversion permission to combat this turn.
            Assert.That(actions.ConvertMoveToAttack(context, 1), Is.False);
            Assert.That(actions.ConvertMoveToRanged(context, 1), Is.False);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(context.MeleePool + context.RangedPool, Is.Zero);

            var movement = new MovementService();
            var first = new AxialCoord(1, 0);
            Assert.That(MovementService.GetCost(first, context.MovementMap, DayPart.Day, context), Is.EqualTo(2));
            Assert.That(movement.TryMoveUsingPool(player, first, context.MovementMap, context), Is.True);
            Assert.That(player.Position, Is.EqualTo(first));
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 2 : 0));
            if (strong)
            {
                Assert.That(movement.TryMoveUsingPool(player, new AxialCoord(2, 0), context.MovementMap, context), Is.True);
                Assert.That(player.Position, Is.EqualTo(new AxialCoord(2, 0)));
                Assert.That(context.MovementPool, Is.Zero);
            }
            PlayedExactly(player, agility);
        }

        [Test]
        public void BaseMovementConvertsToActualMeleeAndCombinesWithOriginalFury()
        {
            var player = new PlayerState();
            var context = new ActionContext
            {
                InBattle = true, CombatPhase = Phase.Melee,
                Enemies = new List<Monster> { Enemy("monster_013") }
            };
            var agility = Add(player, "advanced_card_006");
            var fury = Add(player, "basic_card_021");
            Play(player, context, agility);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            var actions = new ActionSystem(false);
            Assert.That(actions.ConvertMoveToRanged(context, 1), Is.False);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(actions.ConvertMoveToAttack(context, 2), Is.True);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(2));
            Play(player, context, fury);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            var attack = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Melee);
            AssertAttack(attack, 4);
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.MeleePool + context.RangedPool + context.MovementPool, Is.Zero);
            PlayedExactly(player, agility, fury);
        }

        [Test]
        public void StrongCanMixRangedAndMeleeConversionsAndConsumeBothAgainstOriginalEnemies()
        {
            var player = new PlayerState();
            var context = new ActionContext
            {
                InBattle = true, CombatPhase = Phase.Ranged,
                Enemies = new List<Monster> { Enemy("monster_007"), Enemy("monster_013") }
            };
            var agility = Add(player, "advanced_card_006");
            var strongMarch = Add(player, "basic_card_000");
            var baseMarch = Add(player, "basic_card_000");
            var fury = Add(player, "basic_card_021");
            Assert.That(baseMarch, Is.Not.SameAs(strongMarch));
            player.Mana.AddToken(ManaColor.White);
            player.Mana.AddToken(ManaColor.Green);
            Play(player, context, agility, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Play(player, context, strongMarch, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(8));
            Play(player, context, baseMarch);
            Assert.That(context.MovementPool, Is.EqualTo(10));

            var actions = new ActionSystem(false);
            Assert.That(actions.ConvertMoveToRanged(context, 4), Is.True);
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(context.RangedPool, Is.EqualTo(4));
            var ranged = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Ranged);
            AssertAttack(ranged, 4);
            Assert.That(player.Fame, Is.EqualTo(3));
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(2));

            CardCombatActions.EnterPhase(context, Phase.Melee);
            Assert.That(actions.ConvertMoveToAttack(context, 2), Is.True);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(2));
            Play(player, context, fury);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            var melee = CardCombatActions.Attack(player, context, context.Enemies, new[] { 1 }, Phase.Melee);
            AssertAttack(melee, 4);
            Assert.That(player.Fame, Is.EqualTo(5));
            Assert.That(context.MeleePool + context.RangedPool + context.MovementPool, Is.Zero);
            Assert.That(context.DefeatedEnemyIndices.SetEquals(new[] { 0, 1 }), Is.True);
            PlayedExactly(player, agility, strongMarch, baseMarch, fury);
        }
    }
}
