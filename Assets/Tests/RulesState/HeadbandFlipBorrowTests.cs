using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandFlipBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(int index)
        {
            var p=new PlayerState{Fame=8};var c=new ActionContext{SkillSupply=new SkillSupply(),SkipDraw=true};
            var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[index];c.SkillSupply.Offer.Add(s);return(p,c,h,s);
        }
        private void Invariant(PlayerState p,ActionContext c,DeedCard h,SkillCard s,bool ownFlip=false)
        {
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
            Assert.That(p.FlippedSkills.Contains(s.SourceId),Is.EqualTo(ownFlip));
            Assert.That(SkillActions.IsUsed(p,s),Is.EqualTo(ownFlip));Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
            Assert.That(p.Skills,Is.Empty);
        }
        [TestCase(0,false)] [TestCase(1,false)] [TestCase(0,true)] [TestCase(1,true)]
        public void FrenzyFourActualAttackConsumedWithoutPersonalFlip(int option,bool ownFlip)
        {
            var(p,c,h,s)=Setup(2);c.InBattle=true;c.CombatPhase=Phase.Melee;
            c.Enemies=new[]{CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032")};
            if(ownFlip)p.FlippedSkills.Add(s.SourceId);
            HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,option);Assert.That(c.MeleePool,Is.EqualTo(4));
            Invariant(p,c,h,s,ownFlip);
            Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Melee).Kills,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(8+c.Enemies[0].Fame));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,option));
        }
        [TestCase(false)] [TestCase(true)]
        public void RitualOneRedTokenPaysRealOriginalRageWithoutPersonalFlip(bool ownFlip)
        {
            var(p,c,h,s)=Setup(3);if(ownFlip)p.FlippedSkills.Add(s.SourceId);
            HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,(int)ManaColor.Red);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(p.Mana.Crystals,Is.Empty);
            Invariant(p,c,h,s,ownFlip);
            c.InBattle=true;c.CombatPhase=Phase.Melee;
            var rage=new DeedCard("basic_card_021",CardType.Action);p.Deck.Hand.Add(rage);
            CardPlayActions.Play(p,c,rage,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==rage.Id),true);
            Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(c.MeleePool,Is.EqualTo(4));
        }
        [TestCase(0)] [TestCase(1)]
        public void DisguiseRealRecruitmentAndEndTurnKeepPersonalState(int option)
        {
            var(p,c,h,s)=Setup(5);p.Reputation=option==1?-3:0;
            // Original Promise supplies real influence for the flip branch.
            if(option==1){var promise=new DeedCard("basic_card_008",CardType.Action);p.Deck.Hand.Add(promise);p.Mana.AddToken(ManaColor.White);
                CardPlayActions.Play(p,c,promise,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==promise.Id),true);}
            int before=c.InfluencePool;RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,option);
            Assert.That(c.InfluencePool,Is.EqualTo(option==1?before:4+c.InteractionAppliedModifier));
            Assert.That(c.IgnoreReputationTrackEffects,Is.EqualTo(option==1));Assert.That(p.FlippedSkills,Is.Empty);
            var offer=new List<UnitCard>{CardJsonLoader.LoadUnit("army_2_006")};var unit=offer[0];int available=c.InfluencePool;
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);
            Assert.That(c.InfluencePool,Is.EqualTo(available-unit.InfluenceCost));Assert.That(p.Units.Single().Card,Is.SameAs(unit));
            Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));Assert.That(SkillActions.IsUsed(p,s),Is.False);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);Assert.That(c.IgnoreReputationTrackEffects,Is.False);
            Assert.That(p.FlippedSkills,Is.Empty);Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,h)),Is.True);
        }
        [TestCase(2,100)] [TestCase(2,2)] [TestCase(3,100)] [TestCase(3,99)] [TestCase(5,100)] [TestCase(5,2)]
        public void InvalidChoicePreservesAllState(int index,int option)
        {
            var(p,c,h,s)=Setup(index);c.InBattle=index==2;c.CombatPhase=Phase.Melee;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,option));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Single(),Is.SameAs(h));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(c.MeleePool,Is.Zero);Assert.That(c.InfluencePool,Is.Zero);Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(p.FlippedSkills,Is.Empty);
        }
        [Test] public void DisguiseBorrowKeepsExistingPersonalFlipAndRejectsBadTiming()
        {
            var(p,c,h,s)=Setup(5);p.FlippedSkills.Add(s.SourceId);c.RestedThisTurn=true;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,1));
            Assert.That(c.IgnoreReputationTrackEffects,Is.False);Assert.That(p.Deck.Hand.Single(),Is.SameAs(h));
            c.RestedThisTurn=false;HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,1);
            Assert.That(c.IgnoreReputationTrackEffects,Is.True);Invariant(p,c,h,s,true);
        }
        [Test] public void FrenzyRejectsOutsideMeleeWithoutPartialPower()
        {
            var(p,c,h,s)=Setup(2);c.InBattle=true;c.CombatPhase=Phase.Block;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,1));
            Assert.That(c.MeleePool,Is.Zero);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.FlippedSkills,Is.Empty);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(h));
        }
        [Test] public void NormalSocialFirstCannotThenFlipAndLegacyCompletesOnlyNormal()
        {
            var(p,c,h,s)=Setup(5);HeadbandBorrowResolution.UseSocial(p,c,h,s,0);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseFlipSkill(p,c,h,s,1));
            Assert.That(c.InfluencePool,Is.EqualTo(2));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            HeadbandBorrowResolution.UseSocial(p,c,h,s,0);Assert.That(c.InfluencePool,Is.EqualTo(4));Invariant(p,c,h,s);
        }
        [TestCase(2)] [TestCase(3)] [TestCase(5)]
        public void GenericHeadbandCannotPartiallyCommitFlip(int index)
        {
            var(p,c,h,s)=Setup(index);c.InBattle=true;c.CombatPhase=Phase.Melee;c.HeadbandSkillOption=1;
            Assert.Throws<InvalidOperationException>(()=>new HeadbandEffect(false).Execute(p,c,0));
            Assert.That(c.MeleePool,Is.Zero);Assert.That(p.FlippedSkills,Is.Empty);Assert.That(p.Mana.GetTotal(),Is.Zero);
        }
    }
}
