using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Cards
{
    public sealed class HeadbandWoundUnitBorrowTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard h,SkillCard s) Setup(string pool,int index)
        {
            var p=new PlayerState{Fame=5};p.Deck.SetDeck(new[]{new DeedCard("basic_card_000",CardType.Action),new DeedCard("basic_card_001",CardType.Action)});
            var c=new ActionContext{SkillSupply=new SkillSupply()};c.OtherPlayers.Add(new PlayerState{Fame=5});
            var h=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(h);
            var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_"+pool)[index];c.SkillSupply.Offer.Add(s);return(p,c,h,s);
        }
        [TestCase("001",8,ManaColor.Green)] [TestCase("003",4,ManaColor.Red)]
        public void RegenerationUsesIndependentWoundsColorsAndRestoresInputs(string pool,int index,ManaColor firstColor)
        {
            var(p,c,h,s)=Setup(pool,index);p.Deck.GainWound();p.Deck.GainWound();var wounds=p.Deck.Hand.Where(x=>x.Type==CardType.Wound).ToArray();
            var sentinel=new DeedCard("w",CardType.Wound);c.SelectedSkillWound=sentinel;p.Mana.AddToken(firstColor);p.Mana.AddCrystal(ManaColor.Blue);c.InBattle=true;
            HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[1],firstColor);
            Assert.That(c.SelectedSkillWound,Is.SameAs(sentinel));Assert.That(c.PendingHeadbandBorrow.Remaining,Is.EqualTo(1));
            Assert.That(p.Deck.Hand.Contains(wounds[0]),Is.True);Assert.That(p.Deck.Hand.Any(x=>ReferenceEquals(x,wounds[1])),Is.False);
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[1],ManaColor.Blue));
            Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));Assert.That(c.SelectedSkillWound,Is.SameAs(sentinel));
            HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[0],ManaColor.Blue);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Wounds,Is.Zero);Assert.That(p.Mana.GetTotal(),Is.Zero);
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
            Assert.That(c.HealedHandWoundsThisTurn,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Skills,Is.Empty);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));
        }
        [Test] public void RegenerationFailureAndAvailabilityAreReadOnlyAndNoManaAllowsFinish()
        {
            var(p,c,h,s)=Setup("001",8);p.Deck.GainWound();p.Deck.GainWound();var wounds=p.Deck.Hand.Where(x=>x.Type==CardType.Wound).ToArray();
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[0],ManaColor.Blue));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);
            p.Mana.AddToken(ManaColor.Blue);HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[0],ManaColor.Blue);
            var pending=c.PendingHeadbandBorrow;int draws=p.Deck.DrawPileCount;Assert.That(pending.HasLegalInput(),Is.False);
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[1],ManaColor.Blue));
            Assert.That(pending.Remaining,Is.EqualTo(1));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(draws));Assert.That(c.SelectedSkillWound,Is.Null);
            p.Mana.AddToken(ManaColor.White);Assert.That(pending.HasLegalInput(),Is.True);Assert.That(p.Mana.GetTotal(),Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.FinishIfImpossible(p,c));
            HeadbandBorrowResolution.UseWound(p,c,h,s,wounds[1],ManaColor.White);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }
        [Test] public void RegenerationCanFinishWhenRemainingWoundHasNoPayableColor()
        {
            var(p,c,h,s)=Setup("003",4);p.Deck.GainWound();p.Deck.GainWound();p.Mana.AddToken(ManaColor.Blue);
            HeadbandBorrowResolution.UseWound(p,c,h,s,p.Deck.Hand.First(x=>x.Type==CardType.Wound),ManaColor.Blue);
            HeadbandBorrowResolution.FinishIfImpossible(p,c);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Wounds,Is.EqualTo(1));
        }
        [Test] public void FearlessUsesExactDiscardThenFreshlyDrawnWoundAndKeepsLegacyDefault()
        {
            var(p,c,h,s)=Setup("007",5);var drawn=new DeedCard("w",CardType.Wound);p.Deck.SetDeck(new[]{drawn,new DeedCard("basic_card_000",CardType.Action)});p.Deck.Hand.Add(h);p.Deck.GainWound();var first=p.Deck.Hand.Single(x=>x.Type==CardType.Wound);
            HeadbandBorrowResolution.UseWound(p,c,h,s,first);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(first));Assert.That(p.Deck.Hand.Contains(drawn),Is.True);
            Assert.That(c.PendingHeadbandBorrow.HasLegalInput(),Is.True);HeadbandBorrowResolution.UseWound(p,c,h,s,drawn);
            Assert.That(p.Deck.DiscardPile.First(),Is.SameAs(drawn));Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
        [Test] public void FearlessRejectsBattleFakeInputAndExtraneousCostBeforePlaying()
        {
            var(p,c,h,s)=Setup("007",5);p.Deck.GainWound();var wound=p.Deck.Hand.Single(x=>x.Type==CardType.Wound);c.InBattle=true;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,wound));c.InBattle=false;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,new DeedCard(wound.Id,CardType.Wound)));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseWound(p,c,h,s,wound,ManaColor.Blue));
            Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(c.SelectedSkillWound,Is.Null);Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void RallyIsOneUseAndKeepsReadinessSeparateFromWounds(bool heal)
        {
            var(p,c,h,s)=Setup("002",2);var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));p.Units.Add(unit);unit.Exhaust();unit.AddWounds(1);
            var sentinel=new UnitState(unit.Card);c.TargetUnit=sentinel;HeadbandBorrowResolution.UseUnit(p,c,h,s,unit,heal);
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(c.TargetUnit,Is.SameAs(sentinel));Assert.That(unit.IsReady,Is.EqualTo(!heal));Assert.That(unit.Wounds,Is.EqualTo(heal?0:1));
            Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.SkillSupply.Offer.Single(),Is.SameAs(s));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(h));
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseUnit(p,c,h,s,unit,!heal));
            if(heal)Assert.That(c.HealedUnitsThisTurn.Contains(unit),Is.True);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void InvalidRallyPreservesPhysicalHeadband(int invalid)
        {
            var(p,c,h,s)=Setup("002",2);var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));if(invalid!=0)p.Units.Add(unit);
            if(invalid==1)c.InBattle=true;if(invalid==2)unit.Exhaust();if(invalid==4)unit.Destroy();bool heal=invalid==2;
            Assert.Throws<InvalidOperationException>(()=>HeadbandBorrowResolution.UseUnit(p,c,h,s,unit,heal));
            Assert.That(c.PendingHeadbandBorrow,Is.Null);Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(c.TargetUnit,Is.Null);Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }

        [Test] public void LegacyHeadbandCannotLoopDynamicSkillWithOneStaleInput()
        {
            var(p,c,h,s)=Setup("001",8);p.Deck.GainWound();p.Deck.GainWound();p.Mana.AddToken(ManaColor.Green,2);
            c.SelectedSkillWound=p.Deck.Hand.First(x=>x.Type==CardType.Wound);c.HeadbandSkillOption=(int)ManaColor.Green;
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,h,CardJsonLoader.LoadItems().Single(x=>x.Id==h.Id),false,0));
            Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(p.Mana.GetTotal(),Is.EqualTo(2));Assert.That(p.Deck.Hand.Contains(h),Is.True);Assert.That(c.PendingHeadbandBorrow,Is.Null);
        }

        [Test] public void RallyReadiedSameUnitActivatesOriginalAttackAndDefeatsActualEnemy()
        {
            var(p,c,h,s)=Setup("002",2);var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));p.Units.Add(unit);unit.Exhaust();
            HeadbandBorrowResolution.UseUnit(p,c,h,s,unit,false);
            var battle=new MK.Logic.Runtime.Battles.PveBattleSession(p,new[]{CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032")},CardJsonLoader.LoadBasicActions());
            battle.Advance();battle.Advance();battle.Advance();
            CardUnitActions.Use(p,battle.Context,unit,0);Assert.That(unit.IsReady,Is.False);Assert.That(battle.Context.MeleePool,Is.EqualTo(5));
            battle.Attack(0);Assert.That(battle.Context.DefeatedEnemyIndices.Count,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);
        }
    }
}
