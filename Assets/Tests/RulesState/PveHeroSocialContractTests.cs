using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveHeroSocialContractTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private SkillCard Own(PlayerState p,bool disguise){var s=CardJsonLoader.LoadHeroSkills(disguise?"hero_skill_pool_003":"hero_skill_pool_004")[disguise?5:3];p.Skills.Add(s);return s;}
        private List<UnitCard> Offer()=>new List<UnitCard>{CardJsonLoader.LoadUnit("army_2_006")};
        [TestCase(-7)] [TestCase(-3)] [TestCase(3)]
        public void DisguiseIgnoresTrackForActualRecruitmentButRealReputationStillChanges(int reputation)
        {
            var p=new PlayerState{Fame=8,Reputation=reputation};var s=Own(p,true);var c=new ActionContext();var offer=Offer();var original=offer[0];
            SkillActions.Use(p,c,s,1);RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            Assert.That(c.InfluencePool,Is.Zero);c.InfluencePool=original.InfluenceCost;c.ReputationPerRecruit=1;
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.Zero);
            Assert.That(p.Units.Single().Card,Is.SameAs(original));Assert.That(p.Reputation,Is.EqualTo(reputation+1));Assert.That(p.FlippedSkills.Contains(s.SourceId),Is.True);
        }
        [TestCase(-3)] [TestCase(3)]
        public void MidInteractionFlipReconcilesAlreadyAppliedTrackModifierExactlyOnce(int reputation)
        {
            var p=new PlayerState{Reputation=reputation};var s=Own(p,true);var c=new ActionContext{InfluencePool=5};
            RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);Assert.That(c.InfluencePool,Is.Not.EqualTo(5));
            SkillActions.Use(p,c,s,1);Assert.That(c.InfluencePool,Is.EqualTo(5));Assert.That(c.InteractionAppliedModifier,Is.Zero);
            p.Mana.AddToken(ManaColor.Green);SkillActions.Use(p,c,s,100);
            Assert.That(c.InfluencePool,Is.EqualTo(5));Assert.That(c.IgnoreReputationTrackEffects,Is.True);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,0));Assert.That(p.Mana.GetTotal(),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void FlipAfterOriginalSpecialRecruitCancelsActualReversedOrDoubledModifier(bool doubled)
        {
            var p=new PlayerState{Fame=15,Reputation=3};var skill=Own(p,true);var c=new ActionContext{InfluencePool=30};
            var unit=CardJsonLoader.LoadUnit(doubled?"army_008":"army_2_011");
            Assert.That(doubled?unit.DoubleRecruitReputation:unit.ReverseRecruitReputation,Is.True);
            var offer=new List<UnitCard>{unit};RecruitmentService.BeginInteraction(p,c,RecruitLocation.RefugeeCamp);
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.RefugeeCamp),Is.True);
            int before=c.InfluencePool, applied=c.InteractionAppliedModifier;
            SkillActions.Use(p,c,skill,1);Assert.That(c.InfluencePool,Is.EqualTo(before-applied));Assert.That(c.InteractionAppliedModifier,Is.Zero);
            Assert.That(doubled?c.InteractionDoubled:c.InteractionReversed,Is.True);
        }
        [Test] public void MissingRestoreFeePreservesFlipUsageAndActualGreenPaymentDoesNotResetTurn()
        {
            var p=new PlayerState();var s=Own(p,true);var c=new ActionContext();SkillActions.Use(p,c,s,1);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,100));Assert.That(p.FlippedSkills.Contains(s.SourceId),Is.True);
            p.Mana.AddCrystal(ManaColor.Green);SkillActions.Use(p,c,s,100);Assert.That(p.FlippedSkills,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.True);
            Assert.That(c.IgnoreReputationTrackEffects,Is.True);Assert.That(p.Mana.GetTotal(),Is.Zero);
        }
        [Test] public void FormalEndTurnClearsPermissionButPersistentFlipRequiresPaidRestoreNextTurn()
        {
            var p=new PlayerState{Reputation=-7};var s=Own(p,true);var c=new ActionContext{SkipDraw=true};var engine=new PlayerTurnEngine();engine.StartTurn(p,DayPart.Day);
            SkillActions.Use(p,c,s,1);engine.EndTurn(p,DayPart.Day,null,c);
            Assert.That(c.IgnoreReputationTrackEffects,Is.False);Assert.That(p.FlippedSkills.Contains(s.SourceId),Is.True);
            engine.StartTurn(p,DayPart.Day);Assert.Throws<InvalidOperationException>(()=>RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village));
            p.Mana.AddToken(ManaColor.Green);SkillActions.Use(p,c,s,100);SkillActions.Use(p,c,s,0);
            Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(p.Reputation,Is.EqualTo(-7));
        }
        [TestCase(false)] [TestCase(true)]
        public void LoneThreeRejectsInfluenceRecruitEvenWhenDiscountedToZeroAndPreservesSupply(bool zero)
        {
            var p=new PlayerState();var s=Own(p,false);var c=new ActionContext();var offer=Offer();var card=offer[0];
            SkillActions.Use(p,c,s,1);RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);if(zero)c.RecruitDiscount=99;else c.InfluencePool=card.InfluenceCost;
            int pool=c.InfluencePool;Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.False);
            Assert.That(c.InfluencePool,Is.EqualTo(pool));Assert.That(offer.Single(),Is.SameAs(card));Assert.That(p.Units,Is.Empty);
            Assert.That(new RecruitmentService().TryRecruit(p,card,RecruitLocation.Village,99,c),Is.False);
        }
        [Test] public void ActualPriorInfluenceRecruitPreventsThreeButOneRemainsLegal()
        {
            var p=new PlayerState{Fame=8};var s=Own(p,false);var c=new ActionContext();var offer=Offer();c.InfluencePool=offer[0].InfluenceCost;
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,1));Assert.That(SkillActions.IsUsed(p,s),Is.False);
            SkillActions.Use(p,c,s,0);Assert.That(c.InfluencePool,Is.EqualTo(1));Assert.That(c.InfluenceRecruitmentOccurred,Is.True);
        }
        [Test] public void FreeAcquisitionNeitherBlocksThreeNorBecomesForbiddenByItsPromise()
        {
            var p=new PlayerState{Fame=8};var s=Own(p,false);var c=new ActionContext{UnitOffer=Offer()};
            CardUnitActions.RecruitFree(p,c,0);Assert.That(c.InfluenceRecruitmentOccurred,Is.False);SkillActions.Use(p,c,s,1);
            c.UnitOffer=Offer();var acquired=CardUnitActions.RecruitFree(p,c,0);Assert.That(p.Units.Count,Is.EqualTo(2));Assert.That(p.Units.Contains(acquired),Is.True);Assert.That(c.InfluencePool,Is.EqualTo(3));
        }
        [Test] public void FormalTurnClearsLonePromiseAndNewOneInfluencePaysRealRecruitment()
        {
            var p=new PlayerState();var s=Own(p,false);var c=new ActionContext{SkipDraw=true};var engine=new PlayerTurnEngine();engine.StartTurn(p,DayPart.Day);
            SkillActions.Use(p,c,s,1);engine.EndTurn(p,DayPart.Day,null,c);Assert.That(c.ForbidInfluenceRecruitment,Is.False);
            engine.StartTurn(p,DayPart.Day);SkillActions.Use(p,c,s,0);var offer=Offer();c.InfluencePool+=offer[0].InfluenceCost-1;
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.Zero);
        }
    }
}
