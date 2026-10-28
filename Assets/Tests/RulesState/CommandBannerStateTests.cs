using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CommandBannerStateTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p, ActionContext c, DeedCard b) Fixture()
        {
            var p=new PlayerState();p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var c=new ActionContext { UnitOffer=new List<UnitCard>{CardJsonLoader.LoadUnit("army_2_006")}, SkipDraw=true };
            var b=new DeedCard("items_017",CardType.Artifact);p.Deck.Hand.Add(b);
            return(p,c,b);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard b)
            => CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i=>i.Id==b.Id));
        private static UnitState RecruitWithBanner(PlayerState p,ActionContext c,DeedCard b)
        {
            Play(p,c,b);BannerActions.SelectCommandForRecruit(p,c,b);
            Assert.That(CardUnitActions.TryRecruit(p,c,c.UnitOffer,0,RecruitLocation.Village),Is.True);
            return p.Units.Last();
        }

        [Test] public void InfluenceDoesNotGrantGeneralCommandSlots()
        {
            var(p,c,b)=Fixture();Play(p,c,b);
            Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.TempCommandSlots,Is.Zero);
            Assert.That(p.FreeSlots,Is.Zero);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(b));
            Assert.That(p.BannerCommandCount,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void FullCommandRequiresExplicitActualBannerAndPaysRecruitCost(bool selected)
        {
            var(p,c,b)=Fixture();Play(p,c,b);var offered=c.UnitOffer[0];
            if(selected)BannerActions.SelectCommandForRecruit(p,c,b);
            Assert.That(CardUnitActions.TryRecruit(p,c,c.UnitOffer,0,RecruitLocation.Village),Is.EqualTo(selected));
            Assert.That(c.InfluencePool,Is.EqualTo(selected?0:4));Assert.That(c.UnitOffer.Count,Is.EqualTo(selected?0:1));
            Assert.That(p.FreeSlots,Is.Zero);Assert.That(p.CommandSlots,Is.EqualTo(1));
            Assert.That(p.Units.Count,Is.EqualTo(selected?2:1));Assert.That(p.TempCommandSlots,Is.Zero);
            if(selected)
            {
                Assert.That(p.Units.Last().Card,Is.SameAs(offered));Assert.That(p.Units.Last().Banner,Is.SameAs(b));
                Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(c.CommandBannerForRecruit,Is.Null);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void RecruitmentFailureDoesNotConsumeSelectedBannerOrOffer(bool wrongLocation)
        {
            var(p,c,b)=Fixture();Play(p,c,b);BannerActions.SelectCommandForRecruit(p,c,b);
            if(!wrongLocation)c.InfluencePool=3;
            Assert.That(CardUnitActions.TryRecruit(p,c,c.UnitOffer,0,wrongLocation?RecruitLocation.Monastery:RecruitLocation.Village),Is.False);
            Assert.That(c.InfluencePool,Is.EqualTo(wrongLocation?4:3));Assert.That(c.UnitOffer.Count,Is.EqualTo(1));
            Assert.That(c.CommandBannerForRecruit,Is.SameAs(b));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(b));
            Assert.That(p.Units.Count,Is.EqualTo(1));
        }

        [Test] public void CanAssignAfterEarlierRecruitInSameTurnAndReleaseItsNormalToken()
        {
            var(p,c,b)=Fixture();p.ExtraCommandSlots=1;c.InfluencePool=4;
            Assert.That(CardUnitActions.TryRecruit(p,c,c.UnitOffer,0,RecruitLocation.Village),Is.True);
            var unit=p.Units.Last();unit.Exhaust();Play(p,c,b);BannerActions.AssignCommand(p,c,unit,b);
            Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.FreeSlots,Is.EqualTo(1));
            Assert.That(unit.CommandBannerFaceDown,Is.True);Assert.That(unit.Banner,Is.SameAs(b));
            unit.NewRound();Assert.That(unit.CommandBannerFaceDown,Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void OldForeignDestroyedAndPreviousTurnRecruitsCannotReceiveBanner(int invalid)
        {
            var(p,c,b)=Fixture();var target=p.Units[0];
            if(invalid==1)target=new UnitState(target.Card);
            if(invalid==2)target.Destroy();
            if(invalid==3)
            {
                p.ExtraCommandSlots=1;c.InfluencePool=4;
                Assert.That(CardUnitActions.TryRecruit(p,c,c.UnitOffer,0,RecruitLocation.Village),Is.True);
                target=p.Units.Last();new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            }
            Play(p,c,b);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.AssignCommand(p,c,target,b));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(b));Assert.That(target.Banner,Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void RoundReturnRemovesCommandedUnitEvenWithFreeNormalTokens(bool freeTokens)
        {
            var(p,c,b)=Fixture();var unit=RecruitWithBanner(p,c,b);
            CardUnitActions.Use(p,c,unit,2);Assert.That(unit.CommandBannerFaceDown,Is.True);
            unit.Ready();Assert.That(unit.CommandBannerFaceDown,Is.False);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            new GameEngine(new[]{p},3).EndRound();Assert.That(unit.Banner,Is.SameAs(b));
            if(freeTokens)p.ExtraCommandSlots=2;
            BannerActions.ChooseRoundReturn(p,unit,true);new GameEngine(new[]{p},3).EndRound();
            Assert.That(unit.IsDestroyed,Is.True);Assert.That(p.Units.Contains(unit),Is.False);
            Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(b));Assert.That(p.BannerCommandCount,Is.Zero);
            Assert.That(p.FreeSlots,Is.EqualTo(freeTokens?2:0));
        }

        [TestCase(false)] [TestCase(true)]
        public void DisbandOrDestroyDiscardsOneActualBanner(bool destroy)
        {
            var(p,c,b)=Fixture();var unit=RecruitWithBanner(p,c,b);
            if(destroy)unit.Destroy();else p.Disband(unit);
            unit.Destroy();Assert.That(p.Units.Count,Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(unit.Banner,Is.Null);
        }

        [Test] public void ReplacingCommandWithHonorRemovesUnitAndLeavesNewBannerUnassigned()
        {
            var(p,c,b)=Fixture();var unit=RecruitWithBanner(p,c,b);p.ExtraCommandSlots=2;
            var honor=new DeedCard("items_000",CardType.Artifact);p.Deck.Hand.Add(honor);c.TargetUnit=unit;Play(p,c,honor);
            Assert.That(unit.IsDestroyed,Is.True);Assert.That(unit.Banner,Is.Null);Assert.That(p.Units.Count,Is.EqualTo(1));
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(p.Deck.Hand.Single(),Is.SameAs(honor));
        }

        [Test] public void SelectedBannerWorksWithActualCallToGloryAndCannotBeBurnedWhileAssigned()
        {
            var(p,c,b)=Fixture();Play(p,c,b);BannerActions.SelectCommandForRecruit(p,c,b);
            c.DayPart=DayPart.Night;p.Mana.AddToken(ManaColor.White);p.Mana.AddToken(ManaColor.Black);
            var source=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_020");
            var spell=new DeedCard(source.Id,CardType.Spell);p.Deck.Hand.Add(spell);
            CardPlayActions.Play(p,c,spell,new ActionCardData(source.Id,source.Name,source.Set,source.ImagePath,
                source.EnImagePath,source.Top.Effect,source.Bottom.Effect,source.Top.ManaCost),true);
            Assert.That(p.Units.Last().Banner,Is.SameAs(b));Assert.That(p.FreeSlots,Is.Zero);
            Assert.That(c.InfluencePool,Is.EqualTo(4));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(spell));
            Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i=>i.Id==b.Id),true));
        }

        [Test] public void DisbandingCommandedUnitDoesNotMakeRoomForNormalFreeRecruit()
        {
            var(p,c,b)=Fixture();var unit=RecruitWithBanner(p,c,b);
            c.UnitOffer.Add(CardJsonLoader.LoadUnit("army_2_006"));c.UnitToDisband=unit;
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.RecruitFree(p,c,0));
            Assert.That(unit.IsDestroyed,Is.False);Assert.That(unit.Banner,Is.SameAs(b));Assert.That(c.UnitOffer.Count,Is.EqualTo(1));
        }

        [Test] public void UnusedBannerExpiresAndCannotBeSelectedFromDiscardOrImitated()
        {
            var(p,c,b)=Fixture();Play(p,c,b);
            Assert.Throws<InvalidOperationException>(()=>BannerActions.SelectCommandForRecruit(p,c,new DeedCard(b.Id,b.Type)));
            BannerActions.SelectCommandForRecruit(p,c,b);BannerActions.SelectCommandForRecruit(p,c,null);
            Assert.That(c.CommandBannerForRecruit,Is.Null);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));
            Assert.Throws<InvalidOperationException>(()=>BannerActions.SelectCommandForRecruit(p,c,b));
        }
    }
}
