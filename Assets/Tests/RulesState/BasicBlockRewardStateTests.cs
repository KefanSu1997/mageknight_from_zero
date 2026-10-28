using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BasicBlockRewardStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static ActionContext Battle(string enemyId)
        {
            var enemy = MonsterJsonLoader.LoadMonsters(
                Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == enemyId);
            return new ActionContext
            {
                InBattle = true, CombatPhase = Phase.Block,
                Enemies = new List<Monster> { enemy }
            };
        }

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong, int option = 0)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        private static void AssertBlocked(PlayerState player, ActionContext context,
            CardCombatActions.Result result, int printed, int effective, int required)
        {
            Assert.That(result.PrintedPower, Is.EqualTo(printed));
            Assert.That(result.EffectivePower, Is.EqualTo(effective));
            Assert.That(result.RequiredPower, Is.EqualTo(required));
            Assert.That(result.Wounds, Is.Zero);
            Assert.That(result.Kills, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.WasBlockedThisBattle(context.Enemies[0]), Is.True);
        }

        private static void AttackAndFinish(PlayerState player, ActionContext context,
            int power, int armor, int fame, params DeedCard[] played)
        {
            CardCombatActions.EnterPhase(context, Phase.Melee);
            var attack = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(power));
            Assert.That(attack.EffectivePower, Is.EqualTo(power));
            Assert.That(attack.RequiredPower, Is.EqualTo(armor));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(fame));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(context.DefeatedEnemyIndices.Contains(0), Is.True);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(played.Length));
            for (int i = 0; i < played.Length; i++)
                Assert.That(player.Deck.PlayedCards[i], Is.SameAs(played[i]));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CounterStrikeCountsActualEarlierBlockAndConsumesAttack(bool strong)
        {
            var player = new PlayerState();
            var context = Battle("monster_013");
            var determination = Add(player, "basic_card_016");
            var counterStrike = Add(player, "advanced_card_028");
            player.Mana.AddToken(ManaColor.Blue);
            if (strong) player.Mana.AddToken(ManaColor.Red);

            Play(player, context, determination, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(5));
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 5, 5, 4);
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(counterStrike));

            // This ordinary melee play must count the enemy actually blocked earlier this turn.
            CardCombatActions.EnterPhase(context, Phase.Melee);
            Play(player, context, counterStrike, strong);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(strong ? 7 : 4));
            AttackAndFinish(player, context, strong ? 7 : 4, 4, 2, determination, counterStrike);
        }

        [Test]
        public void CounterStrikeBlockHistorySurvivesBattleAndExpiresAtActualTurnEnd()
        {
            var player = new PlayerState();
            var context = Battle("monster_013");
            var counterStrike = Add(player, "advanced_card_028");
            var determination = Add(player, "basic_card_016");
            player.Mana.AddToken(ManaColor.Blue);
            Play(player, context, counterStrike, false);
            Assert.That(context.MeleePool, Is.EqualTo(2));
            Play(player, context, determination, true);
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 5, 5, 4);
            // Preserve the existing reward for blocks made after Counter Strike was played.
            Assert.That(context.MeleePool, Is.EqualTo(4));
            Assert.That(context.BlockedEnemiesThisTurn, Is.EqualTo(1));

            UnitDamageResolver.EndBattle(player, context);
            Assert.That(context.WasBlockedThisBattle(context.Enemies[0]), Is.False);
            Assert.That(context.BlockedEnemiesThisTurn, Is.EqualTo(1));
            Assert.That(context.AttackBonusPerBlock, Is.EqualTo(2));
            context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(context.BlockedEnemiesThisTurn, Is.Zero);
            Assert.That(context.AttackBonusPerBlock, Is.Zero);
            Assert.That(player.Deck.DiscardPile.Any(c => ReferenceEquals(c, counterStrike)), Is.True);

            new PlayerTurnEngine().StartTurn(player, DayPart.Day);
            context.InBattle = true;
            CardCombatActions.EnterPhase(context, Phase.Melee);
            var nextTurnCard = Add(player, "advanced_card_028");
            Play(player, context, nextTurnCard, false);
            Assert.That(context.MeleePool, Is.EqualTo(2));
            Assert.That(context.BlockedEnemiesThisTurn, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(nextTurnCard));
        }

        [Test]
        public void ShieldBashBaseDoublesItsBlockAgainstSwift()
        {
            var player = new PlayerState();
            var context = Battle("monster_007");
            var shield = Add(player, "advanced_card_031");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Red);

            Play(player, context, shield, false);
            Assert.That(context.BlockPool, Is.EqualTo(3));
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 3, 6, 6);
            Assert.That(CombatMath.EffectiveArmor(context.Enemies[0], 0, context), Is.EqualTo(4));

            CardCombatActions.EnterPhase(context, Phase.Melee);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            AttackAndFinish(player, context, 4, 4, 3, shield, fury);
        }

        [Test]
        public void ConsumedShieldBashDoesNotBoostDeterminationAgainstAnotherSwiftEnemy()
        {
            var player = new PlayerState();
            var context = Battle("monster_007");
            var secondEnemy = MonsterJsonLoader.LoadMonsters(
                Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_007");
            context.Enemies.Add(secondEnemy);
            Assert.That(context.Enemies[0], Is.Not.SameAs(secondEnemy));
            var shield = Add(player, "advanced_card_031");
            var determination = Add(player, "basic_card_016");

            Play(player, context, shield, false);
            var firstBlock = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, firstBlock, 3, 6, 6);
            Assert.That(context.SwiftBlockBonus, Is.Zero);
            Assert.That(context.WasBlockedThisBattle(secondEnemy), Is.False);

            Play(player, context, determination, false, 1);
            Assert.That(context.BlockPool, Is.EqualTo(2));
            Assert.That(context.SwiftBlockBonus, Is.Zero);
            var secondBlock = CardCombatActions.Block(player, context, context.Enemies, 1);
            Assert.That(secondBlock.PrintedPower, Is.EqualTo(2));
            Assert.That(secondBlock.EffectivePower, Is.EqualTo(2));
            Assert.That(secondBlock.RequiredPower, Is.EqualTo(6));
            Assert.That(secondBlock.Wounds, Is.GreaterThan(0));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.SwiftBlockBonus, Is.Zero);
            Assert.That(context.WasBlockedThisBattle(context.Enemies[0]), Is.True);
            Assert.That(context.WasBlockedThisBattle(secondEnemy), Is.False);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(2));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(shield));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(determination));
        }

        [Test]
        public void ShieldBashStrongSwiftExcessReducesActualArmorToOne()
        {
            var player = new PlayerState();
            var context = Battle("monster_007");
            var shield = Add(player, "advanced_card_031");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, shield, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(5));
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 5, 10, 6);
            // Four points of actual excess reduce armor4, bounded below by armor1.
            Assert.That(CombatMath.EffectiveArmor(context.Enemies[0], 0, context), Is.EqualTo(1));

            CardCombatActions.EnterPhase(context, Phase.Melee);
            Play(player, context, fury, false, 0);
            Assert.That(context.MeleePool, Is.EqualTo(2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            AttackAndFinish(player, context, 2, 1, 3, shield, fury);
        }

        [Test]
        public void ShieldBashStrongBlocksOriginalIceResistantEnemyWithoutReducingItsArmor()
        {
            // Rulebook p24: Ice Resistance ignores blue-card non-attack effects.
            // Rulebook p9: combine inefficient physical blocks, then halve once.
            var player = new PlayerState();
            var context = Battle("monster_028");
            var enemy = context.Enemies[0];
            Assert.That(enemy.Attack, Is.EqualTo(5));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Ice));
            Assert.That(enemy.Armor, Is.EqualTo(6));
            Assert.That(enemy.Abilities.Contains(Ability.IceResist), Is.True);
            var shield = Add(player, "advanced_card_031");
            var strongDetermination = Add(player, "basic_card_016");
            var baseDetermination = Add(player, "basic_card_016");
            Assert.That(baseDetermination, Is.Not.SameAs(strongDetermination));
            player.Mana.AddToken(ManaColor.Blue);
            player.Mana.AddToken(ManaColor.Blue);

            Play(player, context, shield, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(context.BlockPool, Is.EqualTo(5));
            Play(player, context, strongDetermination, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(10));
            Play(player, context, baseDetermination, false, 1);
            Assert.That(context.BlockPool, Is.EqualTo(12));

            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 12, 6, 5);
            // The one-point surplus must not apply the blue card's armor reduction.
            Assert.That(CombatMath.EffectiveArmor(enemy, 0, context), Is.EqualTo(6));
            Assert.That(context.ArmorReduction.GetValueOrDefault(0), Is.Zero);
            Assert.That(context.SwiftBlockBonus, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(3));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(shield));
            Assert.That(player.Deck.PlayedCards[1], Is.SameAs(strongDetermination));
            Assert.That(player.Deck.PlayedCards[2], Is.SameAs(baseDetermination));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void ShieldBashStrongNormalExcessDoesNotReceiveSwiftDouble()
        {
            var player = new PlayerState();
            var context = Battle("monster_013");
            var shield = Add(player, "advanced_card_031");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Blue);
            player.Mana.AddToken(ManaColor.Red);

            Play(player, context, shield, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(5));
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            AssertBlocked(player, context, block, 5, 5, 4);
            Assert.That(CombatMath.EffectiveArmor(context.Enemies[0], 0, context), Is.EqualTo(3));

            CardCombatActions.EnterPhase(context, Phase.Melee);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            AttackAndFinish(player, context, 4, 3, 2, shield, fury);
        }
    }
}
