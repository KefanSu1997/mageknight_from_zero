using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveLoyaltyContractTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private SkillCard Skill()=>CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[7];
        private UnitCard Regular()=>CardJsonLoader.LoadUnit("army_2_006");
        private UnitSupply Supply(int n){var s=new UnitSupply();s.SetDeck(Enumerable.Range(0,n).Select(_=>Regular()));return s;}
        [Test] public void AcquisitionAppendsActualRegularCardsOnceWithoutReplacingOfferOrElite()
        {
            var p=new PlayerState();var skill=Skill();var units=new UnitSupply();var cards=Enumerable.Range(0,5).Select(_=>Regular()).ToArray();
            var elite=CardJsonLoader.LoadUnit("army_001");Assert.That(elite.IsElite,Is.True);units.SetDeck(cards.Append(elite));units.StartRound(1,false);var prior=units.Offer.ToArray();
            HeroSkillAcquisition.Acquire(p,skill,units:units);Assert.That(units.Offer.Count,Is.EqualTo(5));Assert.That(units.RemainingCount,Is.EqualTo(1));
            for(int i=0;i<5;i++)Assert.That(units.Offer[i],Is.SameAs(cards[i]));Assert.That(p.CommandSlots,Is.EqualTo(2));Assert.That(p.OrdinaryFreeSlots,Is.EqualTo(1));Assert.That(p.FreeSlots,Is.EqualTo(2));
            HeroSkillAcquisition.Acquire(p,skill,units:units);Assert.That(units.Offer.Count,Is.EqualTo(5));Assert.Throws<InvalidOperationException>(()=>HeroSkillAcquisition.Acquire(p,skill with{},units:units));
            units.StartRound(1,false);Assert.That(units.Offer.Count,Is.EqualTo(3));Assert.That(p.CommandSlots,Is.EqualTo(2));
        }
        [TestCase(0)] [TestCase(1)]
        public void ShortRegularDeckAddsOnlyAvailableAndNeverManufacturesElite(int n)
        {
            var p=new PlayerState();var s=Supply(n);HeroSkillAcquisition.Acquire(p,Skill(),units:s);Assert.That(s.Offer.Count,Is.EqualTo(n));Assert.That(s.RemainingCount,Is.Zero);
        }
        [Test] public void MissingActualSupplyRejectsBeforeSkillOrSlotMutation()
        {
            var p=new PlayerState();Assert.Throws<InvalidOperationException>(()=>HeroSkillAcquisition.Acquire(p,Skill()));Assert.That(p.Skills,Is.Empty);Assert.That(p.CommandSlots,Is.EqualTo(1));
        }
        [Test] public void FormalProgressionAcquiresActualCommonLoyaltyAndAdvancedRewardWithSupply()
        {
            var p=new PlayerState{HeroId="Tovak"};var actions=new AdvActionSupply();actions.SetDeck(CardJsonLoader.LoadAdvancedActions().Select(c=>new AdvActionCard(c.Id)));actions.FillVacancies();
            var common=new SkillSupply();var skill=Skill();common.Offer.Add(skill);var units=Supply(5);units.StartRound(1,false);
            var progression=new HeroProgression(p,CardJsonLoader.LoadHeroSkills("hero_skill_pool_007"),actions,common,()=>true,new Random(5),units);p.Fame=3;progression.PrepareNext();var reward=actions.Offer[0];
            progression.Choose(skill,reward);Assert.That(p.Level,Is.EqualTo(2));Assert.That(p.LoyaltySkill,Is.SameAs(skill));Assert.That(units.Offer.Count,Is.EqualTo(5));
            Assert.That(common.Offer.Any(s=>ReferenceEquals(s,skill)),Is.False);Assert.That(p.Deck.DrawPile.Single().Id,Is.EqualTo(reward.Id));
        }
        [Test] public void ActualHeadbandBurnAcquiresSharedSkillAppendsSupplyAndRemovesPhysicalArtifact()
        {
            var p=new PlayerState();var units=Supply(2);var c=new ActionContext{SkillSupply=new SkillSupply(),UnitSupply=units};var skill=Skill();c.SkillSupply.Offer.Add(skill);
            var item=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(item);var original=CardJsonLoader.LoadItems().Single(i=>i.Id==item.Id);
            CardPlayActions.PlayArtifact(p,c,item,original,true);Assert.That(p.Skills.Single(),Is.SameAs(skill));Assert.That(p.LoyaltySkill,Is.SameAs(skill));
            Assert.That(c.SkillSupply.Offer,Is.Empty);Assert.That(units.Offer.Count,Is.EqualTo(2));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(item));
        }
        [Test] public void HeadbandMissingRealUnitsRejectsBeforeArtifactAndSkillMove()
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply()};var skill=Skill();c.SkillSupply.Offer.Add(skill);
            var item=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(item);var original=CardJsonLoader.LoadItems().Single(i=>i.Id==item.Id);
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,item,original,true));Assert.That(p.Skills,Is.Empty);Assert.That(p.Deck.Hand.Single(),Is.SameAs(item));
            Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(skill));Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(p.CommandSlots,Is.EqualTo(1));
        }
        [Test] public void ExplicitLoyalSlotDiscountBindsOnlyNewUnitAndLeavesOrdinarySlot()
        {
            var p=new PlayerState();var supply=Supply(4);HeroSkillAcquisition.Acquire(p,Skill(),units:supply);
            var c=new ActionContext{UseLoyaltySlot=true};var original=supply.Offer[0];int price=Math.Max(0,original.InfluenceCost-5);c.InfluencePool=price;
            Assert.That(CardUnitActions.TryRecruit(p,c,supply.Offer,0,RecruitLocation.Village),Is.True);var bound=p.Units.Single();Assert.That(bound.Card,Is.SameAs(original));Assert.That(LoyaltyActions.IsBound(p,bound),Is.True);
            Assert.That(c.InfluencePool,Is.Zero);Assert.That(c.UseLoyaltySlot,Is.False);Assert.That(p.OrdinaryFreeSlots,Is.EqualTo(1));Assert.That(p.FreeSlots,Is.EqualTo(1));
            Assert.That(RecruitmentService.GetCost(p,supply.Offer[0],RecruitLocation.Village,c),Is.EqualTo(supply.Offer[0].InfluenceCost));
            c.InfluencePool=supply.Offer[0].InfluenceCost;Assert.That(CardUnitActions.TryRecruit(p,c,supply.Offer,0,RecruitLocation.Village),Is.True);Assert.That(p.FreeSlots,Is.Zero);Assert.That(p.Units[1],Is.Not.SameAs(bound));
        }
        [Test] public void ExpensiveOriginalCardRequiresExactDiscountedPaymentBeforeBinding()
        {
            var p=new PlayerState();var supply=Supply(2);HeroSkillAcquisition.Acquire(p,Skill(),units:supply);
            var original=CardJsonLoader.LoadUnit("army_008");var offer=new List<UnitCard>{original};int price=original.InfluenceCost-5;Assert.That(price,Is.GreaterThan(0));
            var c=new ActionContext{UseLoyaltySlot=true,InfluencePool=price-1};
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.False);Assert.That(c.InfluencePool,Is.EqualTo(price-1));Assert.That(p.LoyaltyUnit,Is.Null);
            c.InfluencePool++;Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.Zero);Assert.That(p.LoyaltyUnit.Card,Is.SameAs(original));
        }
        [Test] public void DefaultOrdinaryRecruitCannotSilentlyUseEmptyLoyalSlot()
        {
            var p=new PlayerState();var s=Supply(4);HeroSkillAcquisition.Acquire(p,Skill(),units:s);p.Units.Add(new UnitState(Regular()));var c=new ActionContext{InfluencePool=99};
            Assert.That(p.FreeSlots,Is.EqualTo(1));Assert.That(p.OrdinaryFreeSlots,Is.Zero);Assert.That(CardUnitActions.TryRecruit(p,c,s.Offer,0,RecruitLocation.Village),Is.False);
            Assert.That(c.InfluencePool,Is.EqualTo(99));Assert.That(s.Offer.Count,Is.EqualTo(2));c.UseLoyaltySlot=true;Assert.That(CardUnitActions.TryRecruit(p,c,s.Offer,0,RecruitLocation.Village),Is.True);
        }
        [Test] public void FreeAcquisitionCanExplicitlyUseLoyaltyButDoesNotAutomaticallyBind()
        {
            var p=new PlayerState();var s=Supply(4);HeroSkillAcquisition.Acquire(p,Skill(),units:s);var c=new ActionContext{UnitOffer=s.Offer};
            var ordinary=CardUnitActions.RecruitFree(p,c,0);Assert.That(LoyaltyActions.IsBound(p,ordinary),Is.False);c.UseLoyaltySlot=true;
            var bound=CardUnitActions.RecruitFree(p,c,0);Assert.That(LoyaltyActions.IsBound(p,bound),Is.True);Assert.That(p.FreeSlots,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>p.Disband(bound));Assert.Throws<InvalidOperationException>(()=>HealingSystem.DismissUnit(p,bound));Assert.That(bound.IsDestroyed,Is.False);
            c.UnitOffer=new List<UnitCard>{Regular()};c.UnitToDisband=bound;Assert.Throws<InvalidOperationException>(()=>CardUnitActions.RecruitFree(p,c,0));Assert.That(p.Units.Contains(bound),Is.True);
        }
        [Test] public void DestroyedBoundUnitReleasesSpecialSlotWithoutCreatingOrdinarySlot()
        {
            var p=new PlayerState();var s=Supply(4);HeroSkillAcquisition.Acquire(p,Skill(),units:s);var c=new ActionContext{UnitOffer=s.Offer,UseLoyaltySlot=true};
            var first=CardUnitActions.RecruitFree(p,c,0);first.Destroy();p.Units.Remove(first);Assert.That(LoyaltyActions.SlotAvailable(p),Is.True);Assert.That(p.OrdinaryFreeSlots,Is.EqualTo(1));
            c.UseLoyaltySlot=true;var replacement=CardUnitActions.RecruitFree(p,c,0);Assert.That(p.LoyaltyUnit,Is.SameAs(replacement));Assert.That(LoyaltyActions.IsBound(p,first),Is.False);
        }
        [Test] public void ConflictingCommandPreservesUnitSupplyAndBinding()
        {
            var p=new PlayerState();var s=Supply(4);HeroSkillAcquisition.Acquire(p,Skill(),units:s);var c=new ActionContext{UseLoyaltySlot=true,InfluencePool=5,UnitOffer=s.Offer};
            var banner=new DeedCard("items_017",CardType.Artifact);p.Deck.Hand.Add(banner);
            CardPlayActions.PlayArtifact(p,c,banner,CardJsonLoader.LoadItems().Single(i=>i.Id==banner.Id));
            BannerActions.SelectCommandForRecruit(p,c,banner);
            Assert.That(CardUnitActions.TryRecruit(p,c,s.Offer,0,RecruitLocation.Village),Is.False);Assert.Throws<InvalidOperationException>(()=>CardUnitActions.RecruitFree(p,c,0));
            Assert.That(p.LoyaltyUnit,Is.Null);Assert.That(s.Offer.Count,Is.EqualTo(2));Assert.That(c.InfluencePool,Is.EqualTo(9));
        }
    }
}
