using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BasicRecruitmentRewardsStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PlayerState Player()
        {
            var player = new PlayerState { TempCommandSlots = 1 };
            var ownedFarmer = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            ownedFarmer.Exhaust();
            player.Units.Add(ownedFarmer);
            return player;
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

        private static void RecruitedExactly(PlayerState player, UnitState previous, UnitCard candidate,
            IList<UnitCard> offer, params DeedCard[] played)
        {
            Assert.That(offer, Is.Empty);
            Assert.That(player.Units.Count, Is.EqualTo(2));
            Assert.That(player.Units[0], Is.SameAs(previous));
            Assert.That(previous.IsReady, Is.False);
            Assert.That(player.Units[1].Card, Is.SameAs(candidate));
            Assert.That(player.Units[1].IsReady, Is.True);
            Assert.That(player.Units[1].Wounds, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(played.Length));
            for (int i = 0; i < played.Length; i++)
                Assert.That(player.Deck.PlayedCards[i], Is.SameAs(played[i]));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Wounds, Is.Zero);
        }

        [Test]
        public void IntimidateBaseDiscountRecruitsOriginalPeasantsAndCostsReputation()
        {
            var player = Player();
            var previous = player.Units.Single();
            var context = new ActionContext();
            var intimidate = Add(player, "basic_card_026");
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            var offer = new List<UnitCard> { candidate };
            Assert.That(candidate.InfluenceCost, Is.EqualTo(4));
            Assert.That(candidate.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village));

            Play(player, context, intimidate);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            Assert.That(context.RecruitDiscount, Is.EqualTo(2));
            Assert.That(context.IntimidateUsed, Is.True);
            Assert.That(player.Reputation, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(intimidate));

            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Village);
            Assert.That(context.InNegotiation, Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(2));
            Assert.That(player.Reputation, Is.Zero);
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Village, context), Is.EqualTo(2));
            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(player.Reputation, Is.EqualTo(-1));
            Assert.That(player.Fame, Is.Zero);
            Assert.That(context.RecruitDiscount, Is.Zero);
            Assert.That(context.IntimidateUsed, Is.False);
            RecruitedExactly(player, previous, candidate, offer, intimidate);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeedsOfGloryRewardsActualRecruitment(bool strong)
        {
            var player = Player();
            var previous = player.Units.Single();
            var context = new ActionContext();
            var glory = Add(player, "advanced_card_014");
            var promise = strong ? null : Add(player, "basic_card_008");
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            var offer = new List<UnitCard> { candidate };
            if (strong) player.Mana.AddToken(ManaColor.White);

            Play(player, context, glory, strong);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 6 : 3));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(player.Reputation, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(glory));
            if (!strong)
            {
                // The original Promise supplies influence2 without adding its own reward.
                Play(player, context, promise);
                Assert.That(context.InfluencePool, Is.EqualTo(5));
            }
            Assert.That(player.Reputation, Is.Zero);
            Assert.That(player.Fame, Is.Zero);

            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Village);
            Assert.That(context.InNegotiation, Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 6 : 5));
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Village, context), Is.EqualTo(4));
            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 2 : 1));
            Assert.That(player.Reputation, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(strong ? 1 : 0));
            if (strong) RecruitedExactly(player, previous, candidate, offer, glory);
            else RecruitedExactly(player, previous, candidate, offer, glory, promise);
        }
    }
}
