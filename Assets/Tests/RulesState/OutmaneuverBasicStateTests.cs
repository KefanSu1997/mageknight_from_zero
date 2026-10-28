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
    public sealed class OutmaneuverBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static Monster Enemy(string id) => MonsterJsonLoader.LoadMonsters(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == id);

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_036")
            {
                Assert.That(original.Name, Is.EqualTo("迂回躲避"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.White }));
            }
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        private static (TurnMachine Machine, ActionContext Context) Begin(PlayerState player, params Monster[] enemies)
        {
            var state = new GameState();
            state.Players.Add(player);
            var context = new ActionContext { Enemies = new List<Monster>(enemies) };
            var machine = new TurnMachine(state);
            machine.BeginBattle(context);
            machine.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Block));
            return (machine, context);
        }

        private static void Defense(TurnMachine machine, PlayerState player, ActionContext context,
            int printedBlock, int newWounds, int totalWounds, int reward)
        {
            var blocks = context.CombatPower.AvailableBlocks(0)
                .Select(b => b with { TargetIndex = 0 }).ToArray();
            Assert.That(blocks.Sum(b => b.Value), Is.EqualTo(printedBlock));
            Assert.That(context.MeleePool, Is.Zero, "The reward is not an immediate attack on card play");
            machine.SetBlock(printedBlock, Element.Physical);
            var result = machine.ResolveDefense(context.Enemies, blocks, context);
            Assert.That(result.TotalWounds, Is.EqualTo(newWounds));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(newWounds));
            Assert.That(player.Wounds, Is.EqualTo(totalWounds));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
            Assert.That(context.MeleePool, Is.EqualTo(reward));
            CardCombatActions.EnterPhase(context, Phase.Melee);
            Assert.That(context.MeleePool, Is.EqualTo(reward), "Re-entering melee cannot grant the reward twice");
        }

        private static void AttackAndFinish(TurnMachine machine, PlayerState player, ActionContext context,
            int power, int wounds, params DeedCard[] played)
        {
            var result = machine.Attack(new[] { 0 }, context);
            Assert.That(result.PrintedPower, Is.EqualTo(power));
            Assert.That(result.EffectivePower, Is.EqualTo(power));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(player.Wounds, Is.EqualTo(wounds));
            Assert.That(context.DefeatedEnemyIndices.Count, Is.EqualTo(1));
            Assert.That(context.DefeatedEnemyIndices.Contains(0), Is.True);
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(played.Length));
            for (int i = 0; i < played.Length; i++)
                Assert.That(player.Deck.PlayedCards[i], Is.SameAs(played[i]));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            machine.FinishBattle(context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.End));
            Assert.That(context.InBattle, Is.False);
            Assert.That(context.MeleePool, Is.Zero);
        }

        [Test]
        public void BaseReducesOriginalAttackAndOldWoundDoesNotPreventBlockedReward()
        {
            var player = new PlayerState();
            player.Deck.GainWound();
            var oldWound = player.Deck.Hand.Single();
            var dodge = Add(player, "advanced_card_036");
            var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Red);
            var enemy = Enemy("monster_013");
            Assert.That(enemy.Attack, Is.EqualTo(4));
            var (machine, context) = Begin(player, enemy);

            Play(player, context, dodge);
            CardCombatActions.ReduceEnemyAttack(context, 0, 0);
            Play(player, context, determination, option: 1);
            Defense(machine, player, context, 2, 0, 1, 1);
            Assert.That(context.WasBlockedThisBattle(enemy), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, oldWound)), Is.True);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            AttackAndFinish(machine, player, context, 5, 1, dodge, determination, fury);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(oldWound));
        }

        [Test]
        public void StrongSingleAttackReductionAwardsTwoThenOriginalFuryKills()
        {
            var player = new PlayerState();
            var dodge = Add(player, "advanced_card_036");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.White);
            var (machine, context) = Begin(player, Enemy("monster_013"));

            Play(player, context, dodge, true, 0);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            CardCombatActions.ReduceEnemyAttack(context, 0, 0);
            Defense(machine, player, context, 0, 0, 0, 2);
            Play(player, context, fury);
            AttackAndFinish(machine, player, context, 4, 0, dodge, fury);
        }

        [Test]
        public void StrongTwoDifferentEnemiesEachLoseTwoAndNewWoundsPreventReward()
        {
            var player = new PlayerState();
            var dodge = Add(player, "advanced_card_036");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.White);
            player.Mana.AddToken(ManaColor.Red);
            var first = Enemy("monster_013");
            var second = Enemy("monster_013");
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(player.Armor, Is.EqualTo(2));
            var (machine, context) = Begin(player, first, second);

            Play(player, context, dodge, true, 1);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            CardCombatActions.ReduceEnemyAttack(context, 0, 0);
            CardCombatActions.ReduceEnemyAttack(context, 1, 0);
            Defense(machine, player, context, 0, 2, 2, 0);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            AttackAndFinish(machine, player, context, 4, 2, dodge, fury);
            Assert.That(context.DefeatedEnemyIndices.Contains(1), Is.False);
            Assert.That(context.Enemies[1], Is.SameAs(second));
        }

        [Test]
        public void StrongTwoAttackSlotsOnOneOriginalEnemyBothReduceWithoutDuplicateFame()
        {
            var player = new PlayerState();
            var dodge = Add(player, "advanced_card_036");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.White);
            var enemy = Enemy("monster_058");
            Assert.That(enemy.AttackCount, Is.EqualTo(2));
            Assert.That(enemy.GetAttack(0).Value, Is.EqualTo(1));
            Assert.That(enemy.GetAttack(1).Value, Is.EqualTo(1));
            Assert.That(enemy.GetAttack(0).Element, Is.EqualTo(Element.Physical));
            Assert.That(enemy.GetAttack(1).Element, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Armor, Is.EqualTo(4));
            Assert.That(enemy.Fame, Is.EqualTo(2));
            var (machine, context) = Begin(player, enemy);

            Play(player, context, dodge, true, 1);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            CardCombatActions.ReduceEnemyAttack(context, 0, 0);
            CardCombatActions.ReduceEnemyAttack(context, 0, 1);
            Assert.That(context.Enemies.Single(), Is.SameAs(enemy));
            Defense(machine, player, context, 0, 0, 0, 2);
            Play(player, context, fury);
            AttackAndFinish(machine, player, context, 4, 0, dodge, fury);
        }

        [Test]
        public void OriginalTwoOnePointAttacksEachCauseOneWoundWithoutReduction()
        {
            var player = new PlayerState();
            Assert.That(player.Armor, Is.EqualTo(2));
            var enemy = Enemy("monster_058");
            Assert.That(enemy.AttackCount, Is.EqualTo(2));
            var (machine, context) = Begin(player, enemy);
            Defense(machine, player, context, 0, 2, 2, 0);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Deck.Hand[0], Is.Not.SameAs(player.Deck.Hand[1]));
            Assert.That(player.Fame, Is.Zero);
            machine.FinishBattle(context);
            Assert.That(context.InBattle, Is.False);
        }

        [Test]
        public void InvalidStrongChoiceIsRejectedBeforeWhitePaymentOrPlayingCard()
        {
            var player = new PlayerState();
            var dodge = Add(player, "advanced_card_036");
            player.Mana.AddToken(ManaColor.White);
            var (_, context) = Begin(player, Enemy("monster_013"));
            Assert.Throws<InvalidOperationException>(() => Play(player, context, dodge, true, 2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(dodge));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(context.AttackReduction, Is.Zero);
            Assert.That(context.AttackReductionTimes, Is.Zero);
            Assert.That(context.AttackIfNoWound, Is.Zero);
            Assert.That(context.MeleePool, Is.Zero);
        }

        [Test]
        public void SelectingSecondOriginalAttackResolvesOnlyItAndPreservesFirstReduction()
        {
            var player = new PlayerState();
            var dodge = Add(player, "advanced_card_036");
            var enemy = Enemy("monster_058");
            Assert.That(enemy.AttackCount, Is.EqualTo(2));
            var (_, context) = Begin(player, enemy);
            Play(player, context, dodge);
            CardCombatActions.ReduceEnemyAttack(context, 0, 0);

            var second = CardCombatActions.Block(player, context, context.Enemies, 0, 1);
            Assert.That(second.PrintedPower, Is.Zero);
            Assert.That(second.RequiredPower, Is.EqualTo(1));
            Assert.That(second.Wounds, Is.EqualTo(1));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(1));
            Assert.That(context.PendingAttackReductions.TryGetValue((0, 0), out int firstReduction), Is.True);
            Assert.That(firstReduction, Is.EqualTo(2));

            var first = CardCombatActions.Block(player, context, context.Enemies, 0, 0);
            Assert.That(first.PrintedPower, Is.Zero);
            Assert.That(first.RequiredPower, Is.Zero);
            Assert.That(first.Wounds, Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(1));
            Assert.That(context.PendingAttackReductions.ContainsKey((0, 0)), Is.False);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(dodge));
            Assert.That(context.Enemies.Single(), Is.SameAs(enemy));
        }
    }
}
