using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeadbandPaymentFinishTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState player, ActionContext context, DeedCard headband, DeedCard wound, SkillCard skill) FirstPayment()
        {
            var player = new PlayerState();
            var context = new ActionContext { SkillSupply = new SkillSupply(), DayPart = DayPart.Day };
            var headband = new DeedCard("items_022", CardType.Artifact);
            var march = new DeedCard("basic_card_000", CardType.Action);
            var material = new DeedCard("basic_card_014", CardType.Action);
            var wound = new DeedCard("w", CardType.Wound);
            player.Deck.Hand.AddRange(new[] { headband, march, material, wound });
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[6];
            context.SkillSupply.Offer.Add(skill);
            ManaSkillPaymentActions.ExecuteBorrowedCard(player, context, headband,
                new ManaSkillPaymentSelection(skill, ManaColor.Green, material), march,
                CardJsonLoader.LoadBasicActions().Single(c => c.Id == march.Id), enhanced: true);
            Assert.That(context.PendingHeadbandBorrow.Remaining, Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(material));
            return (player, context, headband, wound, skill);
        }

        [Test]
        public void OnlyRealWoundRemainsAfterFirstCommittedFeeSoFinishPreservesPayment()
        {
            var (player, context, headband, wound, skill) = FirstPayment();
            var pending = context.PendingHeadbandBorrow;
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(wound));
            Assert.That(pending.HasLegalInput(), Is.False);
            Assert.That(context.PendingHeadbandBorrow, Is.SameAs(pending));
            HeadbandBorrowResolution.FinishIfImpossible(player, context);
            Assert.That(context.PendingHeadbandBorrow, Is.Null);
            Assert.That(pending.Remaining, Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(wound));
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, headband)), Is.EqualTo(1));
            Assert.That(player.Mana.GetTotal(), Is.Zero);
            Assert.That(player.Skills, Is.Empty);
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
            Assert.That(context.SkillSupply.Offer.Single(), Is.SameAs(skill));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemainingCardOrEvenUnsupportedReadyMasterStaysUnknown(bool master)
        {
            var (player, context, headband, wound, skill) = FirstPayment();
            if (master) player.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_005")));
            else player.Deck.Hand.Add(new DeedCard("basic_card_008", CardType.Action));
            var pending = context.PendingHeadbandBorrow;
            var hand = player.Deck.Hand.ToArray();
            var played = player.Deck.PlayedCards.ToArray();
            Assert.Throws<InvalidOperationException>(() => pending.HasLegalInput());
            Assert.Throws<InvalidOperationException>(() => HeadbandBorrowResolution.FinishIfImpossible(player, context));
            Assert.That(context.PendingHeadbandBorrow, Is.SameAs(pending));
            Assert.That(pending.Remaining, Is.EqualTo(1));
            Assert.That(context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Deck.Hand, Is.EqualTo(hand));
            Assert.That(player.Deck.PlayedCards, Is.EqualTo(played));
            Assert.That(player.Mana.GetTotal(), Is.Zero);
            Assert.That(SkillActions.IsUsed(player, skill), Is.False);
            if (master) Assert.That(player.Units.Single().IsReady, Is.True);
        }
    }
}
