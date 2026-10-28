using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HeroesRecruitmentTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static UnitCard Hero => CardJsonLoader.LoadUnit("army_008");
        private static UnitCard Peasants => CardJsonLoader.LoadUnit("army_2_006");
        private static UnitCard Thugs => CardJsonLoader.LoadUnit("army_2_011");
        private static bool Recruit(PlayerState p, ActionContext c, IList<UnitCard> offer, int index = 0)
            => CardUnitActions.TryRecruit(p, c, offer, index, RecruitLocation.Village);

        [TestCase(2, 16, 2)] [TestCase(-2, 20, -2)]
        public void TwoHeroesDoubleFrozenWholeInteractionModifierOnlyOnce(int reputation, int funds, int modifier)
        {
            var p = new PlayerState { Reputation = reputation, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = funds, SkipDraw = true };
            var first = Hero; var second = CardJsonLoader.LoadUnit("army_009");
            var offer = new List<UnitCard> { first, second };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(9));
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(offer, Is.Empty);
            Assert.That(c.InteractionAppliedModifier, Is.EqualTo(modifier));
            Assert.That(c.InteractionDoubled, Is.True); Assert.That(c.InteractionReversed, Is.False);
            Assert.That(p.Units[0].Card, Is.SameAs(first)); Assert.That(p.Units[1].Card, Is.SameAs(second));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.InteractionDoubled, Is.False); Assert.That(c.InteractionReputationModifier, Is.Null);
            p.ExtraCommandSlots += 1; c.InfluencePool = 10;
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, new List<UnitCard> { Thugs }), Is.True, "下一回合应能合法招募暴徒");
        }

        [TestCase(false, 2, 9)] [TestCase(true, 2, 9)]
        [TestCase(false, -2, 5)] [TestCase(true, -2, 5)]
        public void OrdinaryRecruitBeforeOrAfterHeroHasSameInteractionTotal(bool ordinaryFirst, int reputation, int remaining)
        {
            var p = new PlayerState { Reputation = reputation, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 20 };
            var offer = new List<UnitCard> { Hero, Peasants };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer, ordinaryFirst ? 1 : 0), Is.True);
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(remaining)); Assert.That(offer, Is.Empty);
        }

        [Test] public void HeroInfluenceAbilityChangesReputationWithoutRepricingTheInteraction()
        {
            var p = new PlayerState { Reputation = 1, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 18 };
            var offer = new List<UnitCard> { Hero, CardJsonLoader.LoadUnit("army_011") };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer), Is.True);
            CardUnitActions.Use(p, c, p.Units.Single(), 2);
            Assert.That(p.Reputation, Is.EqualTo(2)); Assert.That(c.InfluencePool, Is.EqualTo(14));
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(5)); Assert.That(c.InteractionAppliedModifier, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void HeroesAndThugsCannotBothBeRecruitedInOneInteraction(bool heroesFirst)
        {
            var p = new PlayerState { Reputation = 2, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 40 };
            var offer = new List<UnitCard> { Hero, Thugs };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer, heroesFirst ? 0 : 1), Is.True);
            int balance = c.InfluencePool, modifier = c.InteractionAppliedModifier;
            var remaining = offer.Single(); var owned = p.Units.Single();
            Assert.That(Recruit(p, c, offer), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(balance)); Assert.That(c.InteractionAppliedModifier, Is.EqualTo(modifier));
            Assert.That(offer.Single(), Is.SameAs(remaining)); Assert.That(p.Units.Single(), Is.SameAs(owned));
            Assert.That(c.InteractionDoubled, Is.EqualTo(heroesFirst)); Assert.That(c.InteractionReversed, Is.EqualTo(!heroesFirst));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FailedRecruitDoesNotDoubleModifierOrConsumeOffer(int failure)
        {
            var p = new PlayerState { Reputation = 2, ExtraCommandSlots = failure == 1 ? -1 : 0 };
            var c = new ActionContext { InfluencePool = failure == 0 ? 6 : 20 };
            var offer = new List<UnitCard> { Hero };
            var location = failure == 2 ? RecruitLocation.Monastery : RecruitLocation.Village;
            RecruitmentService.BeginInteraction(p, c, location);
            int balance = c.InfluencePool;
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, location), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(balance)); Assert.That(c.InteractionAppliedModifier, Is.EqualTo(1));
            Assert.That(c.InteractionDoubled, Is.False); Assert.That(c.HasRecruited, Is.False);
            Assert.That(offer.Count, Is.EqualTo(1)); Assert.That(p.Units, Is.Empty);
        }
    }
}
