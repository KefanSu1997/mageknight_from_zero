using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CombatEndResourceStateTests
    {
        private static Monster Enemy() => new("target", 2, 2, Element.Physical, 3, Array.Empty<Ability>());

        [TestCase(false)] [TestCase(true)]
        public void OriginalShieldUsesExactCardAndExpiresRemainingBlocksAtBattleEnd(bool burn)
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
            var player = new PlayerState(); var context = new ActionContext { InBattle = true };
            var card = new DeedCard("items_020", CardType.Artifact); player.Deck.Hand.Add(card);
            var original = CardJsonLoader.LoadItems().Single(c => c.Id == card.Id);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.PlayArtifact(player, context, card, original, burn, 2));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(context.BlockPool, Is.Zero);
            CardPlayActions.PlayArtifact(player, context, card, original, burn, 1);
            Assert.That(burn ? player.Deck.RemovedCards.Single() : player.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.That(CardCombatActions.Block(player, context, Enemy()).PrintedPower, Is.EqualTo(4));
            Assert.That(context.BlockPool, Is.EqualTo(burn ? 8 : 4));
            UnitDamageResolver.EndBattle(player, context); context.InBattle = true;
            Assert.That(CardCombatActions.Block(player, context, Enemy()).Wounds, Is.EqualTo(1));
            Assert.That(context.BlockPool, Is.Zero);
            context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(burn ? player.Deck.RemovedCards.Single() : player.Deck.DiscardPile.Single(), Is.SameAs(card));
        }

        [TestCase(AttackType.Melee)] [TestCase(AttackType.Ranged)] [TestCase(AttackType.Siege)]
        public void UnspentAttackCannotKillInNextBattleButNewPowerCan(AttackType type)
        {
            var player = new PlayerState();
            var context = new ActionContext { InBattle = true };
            context.CombatPower.AddAttack(4, Element.Fire, type);
            UnitDamageResolver.EndBattle(player, context);
            context.InBattle = true;
            var phase = type == AttackType.Melee ? Phase.Melee : Phase.Ranged;
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(player, context, new[] { Enemy() }, new[] { 0 }, phase));
            Assert.That(player.Fame, Is.Zero);
            context.CombatPower.AddAttack(2, Element.Ice, type);
            Assert.That(CardCombatActions.Attack(player, context, new[] { Enemy() }, new[] { 0 }, phase).Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(3));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnspentBlocksCannotProtectNextBattleAndNewSplitBlockStillWorks(bool split)
        {
            var player = new PlayerState();
            var context = new ActionContext { InBattle = true };
            if (split)
            {
                context.CombatPower.AddDistinctBlocks(2, Element.ColdFire, 3);
                Assert.That(CardCombatActions.Block(player, context, Enemy()).Wounds, Is.Zero);
            }
            else context.CombatPower.AddBlock(4, Element.Physical);
            UnitDamageResolver.EndBattle(player, context);
            context.InBattle = true;
            var result = CardCombatActions.Block(player, context, Enemy());
            Assert.That(result.PrintedPower, Is.Zero);
            Assert.That(result.Wounds, Is.EqualTo(1));
            context.CombatPower.AddDistinctBlocks(2, Element.ColdFire, 2);
            Assert.That(CardCombatActions.Block(player, context, Enemy()).Wounds, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Block(player, context, Enemy()));
            Assert.That(player.Wounds, Is.EqualTo(1));
        }
    }
}
