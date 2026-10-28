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
    public sealed class DiplomacyBasicStateTests
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

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions()).Single(c => c.Id == card.Id);
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        [TestCase(false, 0, "monster_013", Element.Physical, 4, 0)]
        [TestCase(true, 0, "monster_029", Element.Ice, 3, 1)]
        [TestCase(true, 1, "monster_012", Element.Fire, 4, 0)]
        public void PrintedInfluencePaysForChosenElementAndActuallyBlocks(bool strong, int option,
            string enemyId, Element element, int amount, int remaining)
        {
            var player = new PlayerState();
            var context = new ActionContext
            {
                InBattle = true, CombatPhase = Phase.Block,
                Enemies = new List<Monster> { MonsterJsonLoader.LoadMonsters(
                    Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json")).Single(m => m.Id == enemyId) }
            };
            var diplomacy = Add(player, "advanced_card_018");
            if (strong) player.Mana.AddToken(ManaColor.White);
            Play(player, context, diplomacy, strong, option);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 4 : 2));
            if (!strong) Play(player, context, Add(player, "basic_card_008"));
            Assert.That(context.InfluencePool, Is.EqualTo(4));
            var actions = new ActionSystem(false);
            Assert.That(actions.ConvertInfluenceToBlock(context, 5), Is.False);
            Assert.That(context.InfluencePool, Is.EqualTo(4));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(actions.ConvertInfluenceToBlock(context, amount), Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(remaining));
            Assert.That(context.CombatPower.Blocks.Single().Element, Is.EqualTo(element));
            var block = CardCombatActions.Block(player, context, context.Enemies, 0);
            Assert.That(block.PrintedPower, Is.EqualTo(amount));
            Assert.That(block.EffectivePower, Is.EqualTo(amount));
            Assert.That(block.RequiredPower, Is.EqualTo(amount));
            Assert.That(block.Wounds, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(context.WasBlockedThisBattle(context.Enemies[0]), Is.True);
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.InfluencePool, Is.EqualTo(remaining));
            Assert.That(player.Deck.PlayedCards[0], Is.SameAs(diplomacy));
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(strong ? 1 : 2));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void InvalidStrongElementIsRejectedBeforePaymentOrCardMovement()
        {
            var player = new PlayerState();
            var context = new ActionContext();
            var card = Add(player, "advanced_card_018");
            player.Mana.AddToken(ManaColor.White);
            Assert.Throws<InvalidOperationException>(() => Play(player, context, card, true, 2));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(card));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(context.InfluenceAsBlock, Is.False);
        }

        [Test]
        public void ConversionPermissionsExpireAtRealTurnEnd()
        {
            var player = new PlayerState();
            var context = new ActionContext();
            player.Mana.AddToken(ManaColor.White, 2);
            Play(player, context, Add(player, "advanced_card_006"), true);
            Play(player, context, Add(player, "advanced_card_018"), true, 1);
            var actions = new ActionSystem(false);
            Assert.That(actions.ConvertInfluenceToBlock(context, 1), Is.False);
            Assert.That(context.InfluencePool, Is.EqualTo(4));
            context.SkipDraw = true;
            var turn = new PlayerTurnEngine();
            turn.EndTurn(player, DayPart.Day, null, context);
            turn.StartTurn(player, DayPart.Day);
            Play(player, context, Add(player, "basic_card_008"));
            Play(player, context, Add(player, "basic_card_000"));
            context.InBattle = true;
            Assert.That(actions.ConvertInfluenceToBlock(context, 1), Is.False);
            Assert.That(actions.ConvertMoveToAttack(context, 1), Is.False);
            Assert.That(actions.ConvertMoveToRanged(context, 1), Is.False);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            Assert.That(context.MovementPool, Is.EqualTo(2));
            Assert.That(context.MeleePool + context.RangedPool + context.BlockPool, Is.Zero);
        }
    }
}
