using System;
using System.Collections.Generic;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ThugsRecruitmentTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static UnitCard Thugs => CardJsonLoader.LoadUnit("army_2_011");
        private static UnitCard Farmers => CardJsonLoader.LoadUnit("army_2_006");
        private static bool Recruit(PlayerState p, ActionContext c, IList<UnitCard> offer, int index = 0)
            => CardUnitActions.TryRecruit(p, c, offer, index, RecruitLocation.Village);

        [TestCase(-6, -3)] [TestCase(-5, -3)] [TestCase(-4, -2)] [TestCase(-3, -2)]
        [TestCase(-2, -1)] [TestCase(-1, -1)] [TestCase(0, 0)] [TestCase(1, 0)]
        [TestCase(2, 1)] [TestCase(3, 1)] [TestCase(4, 2)] [TestCase(5, 2)] [TestCase(6, 3)] [TestCase(7, 3)]
        public void PrintedTrackIsNotTheNumberOfSteps(int reputation, int influence)
            => Assert.That(-ReputationTable.CalcRecruitModifier(reputation), Is.EqualTo(influence));

        [TestCase(-2, 0)] [TestCase(2, 0)]
        public void TwoThugsReverseOnceAndEndTurnClearsState(int reputation, int remaining)
        {
            var p = new PlayerState { Reputation = reputation, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = reputation < 0 ? 9 : 11, SkipDraw = true };
            var offer = new List<UnitCard> { Thugs, Thugs };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(c.InfluencePool, Is.EqualTo(reputation < 0 ? 8 : 12));
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(5));
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(remaining)); Assert.That(offer, Is.Empty);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.InteractionReputationModifier, Is.Null); Assert.That(c.InteractionReversed, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void MixedPurchasesHaveTheSameWholeInteractionTotal(bool farmersFirst)
        {
            var p = new PlayerState { Reputation = -2, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 9 };
            var offer = new List<UnitCard> { Thugs, Farmers };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer, farmersFirst ? 1 : 0), Is.True);
            Assert.That(Recruit(p, c, offer), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(1));
            Assert.That(c.InteractionAppliedModifier, Is.EqualTo(1));
            Assert.That(offer, Is.Empty); Assert.That(p.Units.Count, Is.EqualTo(2));
        }

        [Test]
        public void ActualThugsAbilityChangesReputationButNotFrozenModifier()
        {
            var p = new PlayerState { Reputation = 2, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 8 };
            var offer = new List<UnitCard> { Thugs, Thugs };
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(Recruit(p, c, offer), Is.True); Assert.That(c.InfluencePool, Is.EqualTo(2));
            CardUnitActions.Use(p, c, p.Units[0], 2);
            Assert.That(p.Reputation, Is.EqualTo(1)); Assert.That(c.InfluencePool, Is.EqualTo(6));
            Assert.That(Recruit(p, c, offer), Is.True); Assert.That(c.InfluencePool, Is.EqualTo(1));
            Assert.That(c.InteractionAppliedModifier, Is.EqualTo(-1));
        }

        [TestCase(5, 2, 0, RecruitLocation.Village)]
        [TestCase(30, -2, 0, RecruitLocation.Monastery)]
        [TestCase(30, -2, -1, RecruitLocation.Village)]
        public void FailedRecruitCannotReverseOrSpendAnything(int funds, int reputation, int slots, RecruitLocation location)
        {
            var p = new PlayerState { Reputation = reputation, ExtraCommandSlots = slots };
            var c = new ActionContext { InfluencePool = funds, CharmRecruitDiscount = 0 };
            var offer = new List<UnitCard> { Thugs };
            RecruitmentService.BeginInteraction(p, c, location);
            int before = c.InfluencePool, modifier = c.InteractionAppliedModifier;
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, location), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(before)); Assert.That(c.InteractionAppliedModifier, Is.EqualTo(modifier));
            Assert.That(c.InteractionReversed, Is.False); Assert.That(offer.Count, Is.EqualTo(1)); Assert.That(p.Units, Is.Empty);
        }

        [Test]
        public void CannotStartAtXOrFarmBonusAndTurnEndAllowsNewInteraction()
        {
            var p = new PlayerState { Reputation = -7 };
            var c = new ActionContext { InfluencePool = 20, SkipDraw = true };
            Assert.Throws<InvalidOperationException>(() => RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village));
            Assert.That(Recruit(p, c, new List<UnitCard> { Thugs }), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(20)); Assert.That(c.InteractionReputationModifier, Is.Null);
            p.Reputation = 2;
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(c.InfluencePool, Is.EqualTo(21));
            Assert.Throws<InvalidOperationException>(() => RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village));
            RecruitmentService.EndInteraction(c);
            Assert.That(Recruit(p, c, new List<UnitCard> { Thugs }), Is.False);
            Assert.Throws<InvalidOperationException>(() => RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.InteractionReputationModifier, Is.Null); Assert.That(c.InteractionReversed, Is.False);
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.That(c.InfluencePool, Is.EqualTo(1));
        }

        [Test]
        public void NormalBonusAndCharmDiscountAreEachConsumedOnlyOnce()
        {
            var p = new PlayerState { Reputation = 2, ExtraCommandSlots = 1 };
            var c = new ActionContext { InfluencePool = 4, CharmRecruitDiscount = 3 };
            var offer = new List<UnitCard> { Farmers, Farmers };
            Assert.That(Recruit(p, c, offer), Is.True); Assert.That(c.InfluencePool, Is.EqualTo(4));
            Assert.That(c.CharmRecruitDiscount, Is.Zero);
            Assert.That(Recruit(p, c, offer), Is.True); Assert.That(c.InfluencePool, Is.Zero);
        }

        [Test]
        public void RefugeeCampUsesPrintedLocationInsteadOfEliteFlag()
        {
            var p = new PlayerState(); var c = new ActionContext();
            Assert.That(RecruitmentService.GetCost(p, Thugs, RecruitLocation.RefugeeCamp, c), Is.EqualTo(5));
            Assert.That(RecruitmentService.GetCost(p, CardJsonLoader.LoadUnit("army_003"), RecruitLocation.RefugeeCamp, c), Is.EqualTo(9));
        }

        [Test]
        public void AnotherActionAlreadyCommittedCannotBeFollowedByRecruitment()
        {
            var p = new PlayerState(); var c = new ActionContext { InfluencePool = 20, TurnActionCommitted = true };
            Assert.Throws<InvalidOperationException>(() => RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village));
            Assert.That(Recruit(p, c, new List<UnitCard> { Thugs }), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(20)); Assert.That(p.Units, Is.Empty);
        }
    }
}
