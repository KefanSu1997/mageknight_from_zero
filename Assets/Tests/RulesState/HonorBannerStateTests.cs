using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class HonorBannerStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard banner) Fixture()
        {
            var p=new PlayerState{ExtraCommandSlots=1};
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var c=new ActionContext{InBattle=true,TargetUnit=p.Units[0]};
            var banner=new DeedCard("items_000",CardType.Artifact);p.Deck.Hand.Add(banner);
            return(p,c,banner);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard banner,bool burn=false)
            =>CardPlayActions.PlayArtifact(p,c,banner,CardJsonLoader.LoadItems().Single(i=>i.Id=="items_000"),burn);

        [TestCase(0)] [TestCase(1)]
        public void AssignedBannerBoostsAndRewardsOnlyItsUnit(int choice)
        {
            var(p,c,b)=Fixture();Play(p,c,b);
            Assert.That(p.Units[0].Banner,Is.SameAs(b));Assert.That(p.Units[1].Banner,Is.Null);
            Assert.That(p.Deck.Hand,Is.Empty);Assert.That(p.Deck.PlayedCards,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
            CardUnitActions.Use(p,c,p.Units[0],0,choice);Assert.That(p.Fame,Is.EqualTo(1));
            Assert.That(choice==0?c.MeleePool:c.BlockPool,Is.EqualTo(3));
            CardUnitActions.Use(p,c,p.Units[1],0,choice);Assert.That(p.Fame,Is.EqualTo(1));
            Assert.That(choice==0?c.MeleePool:c.BlockPool,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,b,true));
            Assert.That(p.Units[0].Banner,Is.SameAs(b));Assert.That(p.Deck.RemovedCards,Is.Empty);
        }

        [Test] public void BaseFameIsPerActivationButNoExtraFameForSplittingOrHeroAttack()
        {
            var(p,c,b)=Fixture();Play(p,c,b);CardUnitActions.Use(p,c,p.Units[0],0);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(p.Fame,Is.EqualTo(1));
            p.Units[0].Ready();CardUnitActions.Use(p,c,p.Units[0],0);Assert.That(p.Fame,Is.EqualTo(2));
            new ActionSystem().Play(CardJsonLoader.LoadBasicActions().Single(a=>a.Id=="basic_card_021"),p,c,false);
            Assert.That(c.MeleePool,Is.EqualTo(5));Assert.That(p.Fame,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void BannerArmorUsesAssignedUnitOrAllUnitsForBurn(bool burn)
        {
            var(p,c,b)=Fixture();Play(p,c,b,burn);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            Assert.That(UnitDamageResolver.Assign(p,c,p.Units[0],enemy,4,Element.Physical),Is.Zero);
            Assert.That(UnitDamageResolver.Assign(p,c,p.Units[1],enemy,4,Element.Physical),Is.EqualTo(burn?0:1));
            Assert.That(p.Fame,Is.Zero);
        }

        [Test] public void AssignmentDoesNotReadyHealOrExpireAtTurnOrBattleEnd()
        {
            var(p,c,b)=Fixture();p.Units[0].Exhaust();p.Units[0].AddWounds(1);Play(p,c,b);
            Assert.That(p.Units[0].IsReady,Is.False);Assert.That(p.Units[0].Wounds,Is.EqualTo(1));
            UnitDamageResolver.EndBattle(p,c);c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Units[0].Banner,Is.SameAs(b));Assert.That(p.Deck.DiscardPile,Is.Empty);
            new GameEngine(new[]{p},3).EndRound();Assert.That(p.Units[0].Banner,Is.SameAs(b));Assert.That(p.Deck.DrawPile,Is.Empty);
        }

        [Test] public void ReplacementDiscardsOldExactInstanceAndRoundReturnShufflesNewOne()
        {
            var(p,c,b)=Fixture();Play(p,c,b);
            var next=new DeedCard("items_000",CardType.Artifact);p.Deck.Hand.Add(next);Play(p,c,next);
            Assert.That(p.Units[0].Banner,Is.SameAs(next));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));
            BannerActions.ChooseRoundReturn(p,p.Units[0],true);
            Assert.That(p.Units[0].Banner,Is.SameAs(next));
            new GameEngine(new[]{p},3).EndRound();
            Assert.That(p.Units[0].Banner,Is.Null);Assert.That(p.Deck.DrawPile.Count,Is.EqualTo(2));
            Assert.That(p.Deck.DrawPile.Any(x=>ReferenceEquals(x,b)),Is.True);
            Assert.That(p.Deck.DrawPile.Any(x=>ReferenceEquals(x,next)),Is.True);Assert.That(p.Deck.RemovedCards,Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DestroyAndDisbandReleaseBannerExactlyOnce(int route)
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            if(route==0)unit.Destroy();else if(route==1)p.Disband(unit);else HealingSystem.DismissUnit(p,unit);
            Assert.That(unit.Banner,Is.Null);Assert.That(unit.IsDestroyed,Is.True);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));
            unit.Destroy();Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidAttachmentPreservesHandAndOwnedBanner(int invalid)
        {
            var(p,c,b)=Fixture();
            if(invalid==0)c.TargetUnit=null;
            if(invalid==1)c.TargetUnit=new UnitState(p.Units[0].Card);
            if(invalid==2)p.Units[0].Destroy();
            var selected=invalid==3?new DeedCard(b.Id,b.Type):b;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,selected));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(p.Units.All(u=>u.Banner==null),Is.True);
            Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Deck.RemovedCards,Is.Empty);
        }

        [Test] public void BurnRewardsEachUnitOncePerTurnAndKeepsBonusAcrossBattles()
        {
            var(p,c,b)=Fixture();Play(p,c,b,true);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));
            CardUnitActions.Use(p,c,p.Units[0],0);Assert.That(c.MeleePool,Is.EqualTo(3));Assert.That(p.Fame,Is.EqualTo(1));
            p.Units[0].Ready();CardUnitActions.Use(p,c,p.Units[0],0,1);Assert.That(c.BlockPool,Is.EqualTo(3));Assert.That(p.Fame,Is.EqualTo(1));
            UnitDamageResolver.EndBattle(p,c);c.InBattle=true;
            CardUnitActions.Use(p,c,p.Units[1],0);Assert.That(c.MeleePool,Is.EqualTo(3));Assert.That(p.Fame,Is.EqualTo(2));
            Assert.That(c.HonorBannerTurnBonus,Is.EqualTo(1));c.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(c.HonorBannerTurnBonus,Is.Zero);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));
            Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(p.Units.All(u=>u.Banner==null),Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalRegenerationReadiesBetweenBattlesAndHonorCountsCorrectly(bool burn)
        {
            var(p,c,b)=Fixture();Play(p,c,b,burn);CardUnitActions.Use(p,c,p.Units[0],0);
            UnitDamageResolver.EndBattle(p,c);
            var regeneration=CardJsonLoader.LoadAdvancedActions().Single(a=>a.Id=="advanced_card_015");
            var card=new DeedCard(regeneration.Id,CardType.Action);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,regeneration,false);
            Assert.That(p.Units[0].IsReady,Is.True);Assert.That(c.Healing.Total,Is.EqualTo(1));
            c.InBattle=true;Assert.That(c.Healing.Total,Is.Zero);
            CardUnitActions.Use(p,c,p.Units[0],0);
            Assert.That(c.MeleePool,Is.EqualTo(3));Assert.That(p.Fame,Is.EqualTo(burn?1:2));
            Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
        }

        [Test] public void BurnDistinguishesActualOfferCopiesAndDoesNotRewardOneTwice()
        {
            var(p,c,b)=Fixture();Play(p,c,b,true);
            var first=CardJsonLoader.LoadUnit("army_2_006");
            var second=first with { };
            c.UnitOffer=new System.Collections.Generic.List<UnitCard>{first,second};
            var summon=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_020");
            foreach(int index in new[]{0,0,1})
            {
                p.Mana.AddToken(ManaColor.White);
                var instance=new DeedCard(summon.Id,CardType.Spell);p.Deck.Hand.Add(instance);
                CardPlayActions.Play(p,c,instance,new MK.Logic.Data.Cards.ActionCardData(summon.Id,summon.Name,summon.Set,
                    summon.ImagePath,summon.EnImagePath,summon.Top.Effect,summon.Bottom.Effect,summon.Top.ManaCost),false);
                CardUnitActions.UseFromOffer(p,c,index,0);
            }
            Assert.That(p.Fame,Is.EqualTo(2));Assert.That(c.MeleePool,Is.EqualTo(9));
            Assert.That(c.UnitOffer.Count,Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void NonCombatAndFailedAbilitiesDoNotAwardBannerFame(bool burn)
        {
            var(p,c,b)=Fixture();Play(p,c,b,burn);CardUnitActions.Use(p,c,p.Units[0],2);
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(p.Fame,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(p,c,p.Units[0],0));Assert.That(p.Fame,Is.Zero);
        }
    }
}
