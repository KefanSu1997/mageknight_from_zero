using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandPaymentBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(bool polarization=false)
        {
            var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply(),DayPart=DayPart.Day};
            var h=Hand(p,"items_022",CardType.Artifact);var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[polarization?7:6];c.SkillSupply.Offer.Add(s);return(p,c,h,s);
        }
        private static DeedCard Hand(PlayerState p,string id,CardType type=CardType.Action){var a=new DeedCard(id,type);p.Deck.Hand.Add(a);return a;}
        private static void Card(PlayerState p,ActionContext c,DeedCard h,ManaSkillPaymentSelection selection,DeedCard card,bool enhanced=true)
            =>ManaSkillPaymentActions.ExecuteBorrowedCard(p,c,h,selection,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),enhanced);
        [Test] public void InvocationTwoRealPaymentsFeedOriginalMovementAndVillageRecruitment()
        {
            var(p,c,h,s)=Setup();var move=Hand(p,"basic_card_000");var influence=Hand(p,"basic_card_008");var first=Hand(p,"basic_card_014");var second=Hand(p,"basic_card_021");
            Card(p,c,h,new(s,ManaColor.Green,first),move);Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.MovementPool,Is.EqualTo(4));
            Card(p,c,h,new(s,ManaColor.White,second),influence);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.InfluencePool,Is.EqualTo(4));
            Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,first)),Is.True);Assert.That(p.Deck.DiscardPile.Any(x=>ReferenceEquals(x,second)),Is.True);Assert.That(p.Deck.PlayedCards.Count(x=>ReferenceEquals(x,h)),Is.EqualTo(1));
            c.MovementMap=new MapState();var target=new AxialCoord(1,0);c.MovementMap.Placed[p.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
            Assert.That(new MovementService().TryMoveUsingPool(p,target,c.MovementMap,c),Is.True);Assert.That(c.MovementPool,Is.EqualTo(2));
            var offered=CardJsonLoader.LoadUnit("army_2_006");var offer=new List<UnitCard>{offered};RecruitmentService.BeginInteraction(p,c,RecruitLocation.Village);
            Assert.That(CardUnitActions.TryRecruit(p,c,offer,0,RecruitLocation.Village),Is.True);Assert.That(c.InfluencePool,Is.Zero);Assert.That(p.Units.Single().Card,Is.SameAs(offered));
            Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(p.Skills,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
        }
        [Test] public void PolarizationPaysTwoIndependentActualSourceColorsWithoutPersonalUse()
        {
            var(p,c,h,s)=Setup(true);var move=Hand(p,"basic_card_000");var influence=Hand(p,"basic_card_008");p.Mana.AddCrystal(ManaColor.Red);p.Mana.AddToken(ManaColor.Blue);
            Card(p,c,h,new(s,ManaColor.Green,SourceColor:ManaColor.Red),move);Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            Card(p,c,h,new(s,ManaColor.White,SourceColor:ManaColor.Blue),influence);
            Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(c.SpentCrystals.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(c.SpentCrystals.GetValueOrDefault(ManaColor.Green),Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }
        [Test] public void FailedSecondMaterialKeepsFirstPaymentAndRemainingUse()
        {
            var(p,c,h,s)=Setup();var move=Hand(p,"basic_card_000");var influence=Hand(p,"basic_card_008");var first=Hand(p,"basic_card_014");var second=Hand(p,"basic_card_021");
            Card(p,c,h,new(s,ManaColor.Green,first),move);
            Assert.Throws<InvalidOperationException>(()=>Card(p,c,h,new(s,ManaColor.White,first),influence));
            Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(c.InfluencePool,Is.Zero);Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(p.Deck.Hand.Contains(influence),Is.True);
            Assert.That(p.Deck.PlayedCards.Any(x=>ReferenceEquals(x,h)),Is.True);Card(p,c,h,new(s,ManaColor.White,second),influence);Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }
        [TestCase(false)] [TestCase(true)] public void FreeOrNonmatchingOriginalFeeRejectsBeforeAnyEffect(bool wrongColor)
        {
            var(p,c,h,s)=Setup();var card=Hand(p,"basic_card_000");var material=Hand(p,"basic_card_021");
            Assert.Throws<InvalidOperationException>(()=>Card(p,c,h,new(s,wrongColor?ManaColor.White:ManaColor.Green,material),card,wrongColor));
            Assert.That(c.MovementPool,Is.Zero);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);
            Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,h)),Is.True);Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,material)),Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void PhysicalHeadbandCannotBePaymentOrOtherEffectMaterial(int mode)
        {
            var(p,c,h,s)=Setup();var card=Hand(p,"basic_card_000");var material=mode==0?h:Hand(p,"basic_card_021");if(mode==1)c.CardToDiscard=h;if(mode==2)c.CardToRemove=h;
            Assert.Throws<InvalidOperationException>(()=>Card(p,c,h,new(s,ManaColor.Green,material),card));
            Assert.That(c.MovementPool,Is.Zero);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }
        [TestCase(false)] [TestCase(true)] public void ActualUnitPrintedRedFeeUsesBorrowAndExhaustsActualUnit(bool polarization)
        {
            var(p,c,h,s)=Setup(polarization);c.InBattle=true;c.CombatPhase=Phase.Melee;var unit=new UnitState(CardJsonLoader.LoadUnit("army_007"));p.Units.Add(unit);
            var wound=polarization?null:Hand(p,"w",CardType.Wound);if(polarization)p.Mana.AddCrystal(ManaColor.Green);
            ManaSkillPaymentActions.ExecuteBorrowedUnit(p,c,h,new(s,ManaColor.Red,wound,polarization?ManaColor.Green:(ManaColor?)null),unit,1);
            Assert.That(unit.IsReady,Is.False);Assert.That(c.CombatPower.Attacks.Single().Value,Is.EqualTo(6));Assert.That(c.CombatPower.Attacks.Single().SourceUnit,Is.SameAs(unit));Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
        }
        [Test] public void FreeUnitAbilityCannotProduceEffectsThenFailUnconsumed()
        {
            var(p,c,h,s)=Setup(true);c.InBattle=true;c.CombatPhase=Phase.Melee;var unit=new UnitState(CardJsonLoader.LoadUnit("army_007"));p.Units.Add(unit);p.Mana.AddCrystal(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.ExecuteBorrowedUnit(p,c,h,new(s,ManaColor.Red,SourceColor:ManaColor.Green),unit,0));
            Assert.That(unit.IsReady,Is.True);Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);
        }

        [Test] public void PostPaymentFailureKeepsCommittedPhysicalCardAndCannotRefundFirstUse()
        {
            var(p,c,h,s)=Setup();var card=Hand(p,"basic_card_000");var material=Hand(p,"basic_card_021");
            // Controlled corrupt-state fault: original ActionSystem sums attack pools after paying, before the card effect.
            // No substitute effect or public arbitrary callback is introduced for this fault-injection check.
            c.CombatPower.AddAttack(int.MaxValue);c.CombatPower.AddAttack(1);
            Assert.Throws<OverflowException>(()=>Card(p,c,h,new(s,ManaColor.Green,material),card));
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
            Assert.That(c.PendingHeadbandBorrow.Completed,Is.EqualTo(1));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(c.MovementPool,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>Card(p,c,h,new(s,ManaColor.Green,material),card));
            Assert.That(c.PendingHeadbandBorrow.Completed,Is.EqualTo(1));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
    }
}
