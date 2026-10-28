using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CourageBannerStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard b) Fixture()
        {
            var p=new PlayerState{ExtraCommandSlots=1};
            for(int i=0;i<2;i++){var u=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));u.Exhaust();p.Units.Add(u);}
            var c=new ActionContext{TargetUnit=p.Units[0],SkipDraw=true};
            var b=new DeedCard("items_003",CardType.Artifact);p.Deck.Hand.Add(b);return(p,c,b);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard b,bool burn=false)
            =>CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i=>i.Id==b.Id),burn);

        [Test] public void AssignThenExplicitReadyEnablesOneActualUnitAndSpendsOnce()
        {
            var(p,c,b)=Fixture();var unit=p.Units[0];Play(p,c,b);
            Assert.That(unit.Banner,Is.SameAs(b));Assert.That(p.Deck.Hand.Count,Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count,Is.Zero);Assert.That(unit.IsReady,Is.False);
            Assert.That(unit.CourageBannerSpent,Is.False);BannerActions.UseCourage(p,c,unit);
            Assert.That(unit.IsReady,Is.True);Assert.That(p.Units[1].IsReady,Is.False);
            Assert.That(unit.CourageBannerSpent,Is.True);CardUnitActions.Use(p,c,unit,2);
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(unit.IsReady,Is.False);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,unit));
            Assert.That(unit.IsReady,Is.False);Assert.That(c.MovementPool,Is.EqualTo(2));
        }

        [Test] public void CombatCannotActivateOrConsumeButOutsideCombatCan()
        {
            var(p,c,b)=Fixture();c.InBattle=true;Play(p,c,b);var unit=p.Units[0];
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,unit));
            Assert.That(unit.CourageBannerSpent,Is.False);Assert.That(unit.IsReady,Is.False);
            UnitDamageResolver.EndBattle(p,c);BannerActions.UseCourage(p,c,unit);
            Assert.That(unit.CourageBannerSpent,Is.True);Assert.That(unit.IsReady,Is.True);
        }

        [Test] public void UsedStateSurvivesTurnAndResetsOnlyAtNewRound()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];BannerActions.UseCourage(p,c,unit);
            CardUnitActions.Use(p,c,unit,2);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(unit.Banner,Is.SameAs(b));Assert.That(unit.CourageBannerSpent,Is.True);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,unit));
            new GameEngine(new[]{p},3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(unit.CourageBannerSpent,Is.False);Assert.That(unit.IsReady,Is.True);
            CardUnitActions.Use(p,c,unit,2);BannerActions.UseCourage(p,c,unit);
            Assert.That(unit.CourageBannerSpent,Is.True);Assert.That(unit.IsReady,Is.True);
        }

        [TestCase(false)] [TestCase(true)] public void ReadyDoesNotHealOrActivateWoundedUnit(bool burn)
        {
            var(p,c,b)=Fixture();var unit=p.Units[0];unit.AddWounds(2);p.Wounds=1;
            Play(p,c,b,burn);if(!burn)BannerActions.UseCourage(p,c,unit);
            Assert.That(unit.IsReady,Is.True);Assert.That(unit.Wounds,Is.EqualTo(2));
            Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(c.HealedUnitsThisTurn.Count,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(p,c,unit,2));
            Assert.That(c.MovementPool,Is.Zero);
        }

        [Test] public void BurnReadiesAllOwnedUnitsAndRemovesActualArtifactPermanently()
        {
            var(p,c,b)=Fixture();var other=new PlayerState();var foreign=new UnitState(p.Units[0].Card);foreign.Exhaust();other.Units.Add(foreign);c.OtherPlayers.Add(other);
            var destroyed=new UnitState(p.Units[0].Card);destroyed.Destroy();p.Units.Add(destroyed);
            Play(p,c,b,true);
            Assert.That(p.Units.Take(2).All(u=>u.IsReady),Is.True);Assert.That(destroyed.IsReady,Is.False);
            Assert.That(foreign.IsReady,Is.False);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));
            Assert.That(p.Deck.Hand.Count,Is.Zero);Assert.That(p.Deck.PlayedCards.Count,Is.Zero);
            CardUnitActions.Use(p,c,p.Units[0],2);CardUnitActions.Use(p,c,p.Units[1],2);
            Assert.That(c.MovementPool,Is.EqualTo(4));
        }

        [Test] public void CombatBurnRejectsAndPreservesActualHandAndExhaustion()
        {
            var(p,c,b)=Fixture();c.InBattle=true;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(p.Deck.RemovedCards.Count,Is.Zero);
            Assert.That(p.Units.All(u=>!u.IsReady),Is.True);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidAttachmentPreservesHand(int target)
        {
            var(p,c,b)=Fixture();if(target==0)c.TargetUnit=null;
            if(target==1)c.TargetUnit=new UnitState(p.Units[0].Card);
            if(target==2)p.Units[0].Destroy();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(p.Units.All(u=>u.Banner==null),Is.True);
        }

        [Test] public void NonOwnerMissingBannerAndDestroyedTargetsCannotActivate()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(new PlayerState(),c,unit));
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,p.Units[1]));
            Assert.That(unit.CourageBannerSpent,Is.False);unit.Destroy();
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,unit));
            Assert.That(unit.IsReady,Is.False);Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));
        }

        [Test] public void DistinctCopiesSpendIndependentlyAndAssignedBannerCannotBeBurned()
        {
            var(p,c,b)=Fixture();Play(p,c,b);BannerActions.UseCourage(p,c,p.Units[0]);
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
            var next=new DeedCard(b.Id,b.Type);p.Deck.Hand.Add(next);Play(p,c,next);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(p.Units[0].CourageBannerSpent,Is.False);
            BannerActions.UseCourage(p,c,p.Units[0]);Assert.That(p.Units[0].CourageBannerSpent,Is.True);
            Assert.That(p.Deck.RemovedCards.Count,Is.Zero);
        }

        [Test] public void SamePhysicalCardDiscardedAndDrawnDoesNotRefreshWithinRound()
        {
            var(p,c,b)=Fixture();Play(p,c,b);BannerActions.UseCourage(p,c,p.Units[0]);p.Units[0].Destroy();
            p.Deck.ShuffleFromDiscard();p.Deck.DrawExact(1);Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));
            c.TargetUnit=p.Units[1];Play(p,c,b);Assert.That(p.Units[1].CourageBannerSpent,Is.True);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.UseCourage(p,c,p.Units[1]));
            Assert.That(p.Units[1].IsReady,Is.False);
        }

        [Test] public void RoundReturnShufflesActualCardAndNextRoundResetsUsage()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];BannerActions.UseCourage(p,c,unit);
            BannerActions.ChooseRoundReturn(p,unit,true);var game=new GameEngine(new[]{p},3);game.EndRound();
            Assert.That(unit.Banner,Is.Null);Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(b));
            game.StartNewRound(Array.Empty<TacticCard>());Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));
            Play(p,c,b);Assert.That(unit.CourageBannerSpent,Is.False);BannerActions.UseCourage(p,c,unit);
            Assert.That(unit.CourageBannerSpent,Is.True);
        }
    }
}
