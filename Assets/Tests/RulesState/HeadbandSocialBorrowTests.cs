using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandSocialBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static SkillCard Skill(bool disguise)=>CardJsonLoader.LoadHeroSkills(disguise?"hero_skill_pool_003":"hero_skill_pool_004")[disguise?5:3];
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(bool disguise)
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply(),SkipDraw=true};
            var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);var s=Skill(disguise);c.SkillSupply.Offer.Add(s);return(p,c,h,s);
        }
        private static List<UnitCard> Offer()=>new List<UnitCard>{CardJsonLoader.LoadUnit("army_2_006")};
        [TestCase(false)] [TestCase(true)] public void BorrowKeepsOwnedSkillUsageAndPaysRealRecruitment(bool disguise)
        {
            var(p,c,h,s)=Setup(disguise);var own=Skill(!disguise);p.Skills.Add(own);SkillActions.Use(p,c,own,0);int before=c.InfluencePool;
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);Assert.That(c.InfluencePool,Is.EqualTo(before+(disguise?2:1)));
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.InfluencePool,Is.EqualTo(before+(disguise?4:2)));
            var offer=Offer();var original=offer[0];int available=c.InfluencePool;RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.EqualTo(available-original.InfluenceCost));
            Assert.That(p.Units.Single().Card,Is.SameAs(original));Assert.That(p.Skills.Single(),Is.SameAs(own));Assert.That(SkillActions.IsUsed(p,own),Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.False);
            Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));Assert.That(p.FlippedSkills,Is.Empty);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
        }
        [TestCase(0,1)] [TestCase(1,0)] public void LoneIndependentChoicesPayVillageHealingBlockRecruitAndClearAtRealEnd(int first,int second)
        {
            var p=new PlayerState();var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var map=new MapState();map.Placed[p.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Places[p.Position]=new PlaceState(PlaceType.Village);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);j.SelectTactic("day_1");j.BeginTurn();var c=j.Context;var s=Skill(false);j.Game.Globals.Skills.Offer.Add(s);p.Deck.GainWound();j.BeginVillageInteraction();
            HeadbandBorrowResolution.UseSocial(p,c,h,s,first);HeadbandBorrowResolution.UseSocial(p,c,h,s,second);
            Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(c.ForbidInfluenceRecruitment,Is.True);
            var offer=Offer();var original=offer[0];Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.False);Assert.That(offer.Single(),Is.SameAs(original));Assert.That(c.InfluencePool,Is.EqualTo(4));
            j.BuyVillageHealing();j.HealOutsideBattle();Assert.That(p.Wounds,Is.Zero);Assert.That(c.InfluencePool,Is.EqualTo(1));
            j.EndVillageInteraction();j.EndTurn();Assert.That(c.ForbidInfluenceRecruitment,Is.False);Assert.That(c.InfluenceRecruitmentOccurred,Is.False);
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,h)),Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void ActualPriorRecruitRejectsThreeOnSecondUseWithoutRepeatingFirst()
        {
            var(p,c,h,s)=Setup(false);var influence=new DeedCard("basic_card_008",CardType.Action);p.Deck.Hand.Add(influence);p.Mana.AddToken(ManaColor.White);
            CardPlayActions.Play(p,c,influence,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==influence.Id),true);
            RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);Assert.That(CardUnitActions.TryRecruit(p,c,Offer(),0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.Zero);
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSocial(p,c,h,s,1));
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.InfluencePool,Is.EqualTo(1));Assert.That(c.ForbidInfluenceRecruitment,Is.False);Assert.That(c.InfluenceRecruitmentOccurred,Is.True);
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [TestCase(1)] [TestCase(100)] public void DisguiseFlipAndRestoreRejectWithoutChangingPersonalState(int option)
        {
            var(p,c,h,s)=Setup(true);p.Mana.AddCrystal(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSocial(p,c,h,s,option));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));Assert.That(p.FlippedSkills,Is.Empty);Assert.That(c.IgnoreReputationTrackEffects,Is.False);Assert.That(c.InfluencePool,Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidSocialTimingDoesNotPlayHeadband(int invalid)
        {
            var(p,c,h,s)=Setup(false);if(invalid==0)c.RestedThisTurn=true;if(invalid==1){c.InBattle=true;c.CombatPhase=Phase.End;}if(invalid==2)c.TurnActionCommitted=true;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSocial(p,c,h,s,0));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.InfluencePool,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void InvalidSecondChoicePreservesFirstBenefitAndPublicIdentity()
        {
            var(p,c,h,s)=Setup(true);HeadbandBorrowResolution.UseSocial(p,c,h,s,0);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseSocial(p,c,h,s,1));
            Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.FlippedSkills,Is.Empty);Assert.That(c.IgnoreReputationTrackEffects,Is.False);
        }
    }
}
