using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ProtectionBannerStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,DeedCard b) Fixture()
        {
            var p=new PlayerState{ExtraCommandSlots=1};
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));
            var c=new ActionContext{TargetUnit=p.Units[0],SkipDraw=true};
            var b=new DeedCard("items_002",CardType.Artifact);p.Deck.Hand.Add(b);
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);return(p,c,b);
        }
        private static void Play(PlayerState p,ActionContext c,DeedCard b,bool burn=false)
            =>CardPlayActions.PlayArtifact(p,c,b,CardJsonLoader.LoadItems().Single(i=>i.Id==b.Id),burn);
        private static Monster Enemy(string id="monster_025")=>CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);

        [TestCase(Element.Physical,4,0,1)] [TestCase(Element.Fire,4,0,0)]
        [TestCase(Element.Ice,4,0,0)] [TestCase(Element.ColdFire,4,0,0)]
        [TestCase(Element.Physical,9,5,1)] [TestCase(Element.Fire,9,1,1)]
        [TestCase(Element.Ice,9,1,1)] [TestCase(Element.ColdFire,9,1,1)]
        public void ArmorFourAndOnlyFireIceResistanceApplyToAttachedUnit(Element element,int damage,int remaining,int wounds)
        {
            var(p,c,b)=Fixture();c.InBattle=true;Play(p,c,b);var unit=p.Units[0];
            Assert.That(unit.Banner,Is.SameAs(b));Assert.That(c.UnitArmorBonus,Is.Zero);Assert.That(c.UnitsResistAll,Is.False);
            Assert.That(UnitDamageResolver.Assign(p,c,unit,Enemy(),damage,element),Is.EqualTo(remaining));
            Assert.That(unit.Wounds,Is.EqualTo(wounds));
            Assert.Throws<InvalidOperationException>(()=>UnitDamageResolver.Assign(p,c,unit,Enemy(),damage,element));
            Assert.That(UnitDamageResolver.Assign(p,c,p.Units[1],Enemy(),4,element),Is.EqualTo(1));
            Assert.That(p.Units[1].Wounds,Is.EqualTo(1));
        }

        [Test] public void OriginalIceParalysisBelowArmorCannotDestroyAndBonusSurvivesEndBattle()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];var enemy=Enemy("monster_034");c.InBattle=true;
            UnitDamageResolver.Assign(p,c,unit,enemy,enemy.Attack,enemy.AttackElement);
            Assert.That(unit.IsDestroyed,Is.False);Assert.That(unit.Wounds,Is.Zero);
            UnitDamageResolver.EndBattle(p,c);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            c.InBattle=true;UnitDamageResolver.Assign(p,c,unit,enemy,enemy.Attack,enemy.AttackElement);
            Assert.That(unit.Banner,Is.SameAs(b));Assert.That(unit.Wounds,Is.Zero);
        }

        [Test] public void ReplacementRemovesResistanceAndDiscardsActualProtection()
        {
            var(p,c,b)=Fixture();Play(p,c,b);var unit=p.Units[0];
            var courage=new DeedCard("items_003",CardType.Artifact);p.Deck.Hand.Add(courage);Play(p,c,courage);
            Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(b));Assert.That(BannerActions.ProtectionArmor(unit),Is.Zero);
            var enemy=Enemy("monster_034");UnitDamageResolver.Assign(p,c,unit,enemy,enemy.Attack,enemy.AttackElement);
            Assert.That(unit.IsDestroyed,Is.True);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidAttachmentKeepsActualHand(int target)
        {
            var(p,c,b)=Fixture();if(target==0)c.TargetUnit=null;if(target==1)c.TargetUnit=new UnitState(p.Units[0].Card);
            if(target==2)p.Units[0].Destroy();Assert.Throws<InvalidOperationException>(()=>Play(p,c,b));
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(b));Assert.That(c.UnitsResistAll,Is.False);
        }

        [TestCase(false)] [TestCase(true)] public void BurnRemovesOnlyNewHeroWoundsAtEndIncludingPoison(bool burnBeforeDamage)
        {
            var(p,c,b)=Fixture();p.Wounds=1;p.DiscardWounds=1;p.Units[0].AddWounds(2);
            var oldHand=p.Deck.Hand.Single(x=>x.Type==CardType.Wound);var oldDiscard=p.Deck.DiscardPile.Single();
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);c.InBattle=true;
            if(burnBeforeDamage)Play(p,c,b,true);
            int wounds=1;AbilityRules.AssignWounds(Enemy("monster_005"),p,ref wounds);
            if(!burnBeforeDamage)Play(p,c,b,true);
            Assert.That(p.Wounds,Is.EqualTo(2));Assert.That(p.DiscardWounds,Is.EqualTo(2));
            Assert.That(p.Deck.NewWoundsThisTurn,Is.EqualTo(2));Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(b));
            UnitDamageResolver.EndBattle(p,c);Assert.That(p.Wounds,Is.EqualTo(2));
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(oldHand));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(oldDiscard));
            Assert.That(p.Units[0].Wounds,Is.EqualTo(2));Assert.That(c.HealedUnitsThisTurn.Count,Is.Zero);
            Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(3));Assert.That(c.ProtectionBannerBurned,Is.False);
        }

        [Test] public void DrawnOldWoundsRemainAndNewWoundMovedToDiscardIsStillRemoved()
        {
            var(p,c,b)=Fixture();var old=new DeedCard("w",CardType.Wound);p.Deck.PutOnTop(old);
            p.Deck.DrawExact(1);p.AddWoundCards(1);var fresh=p.Deck.Hand.Last();p.Deck.TryDiscardExact(fresh);
            Play(p,c,b,true);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(old));Assert.That(p.Deck.DiscardPile.Count,Is.Zero);
            Assert.That(p.Deck.RemovedCards.Any(x=>ReferenceEquals(x,fresh)),Is.True);
        }

        [Test] public void NewWoundsMovedToDeckOrPlayedAreRemovedOnceWithoutTouchingOldIdenticalCards()
        {
            var(p,c,b)=Fixture();p.Wounds=1;var old=p.Deck.Hand.Last();new PlayerTurnEngine().StartTurn(p,DayPart.Day);
            p.AddWoundCards(2);var first=p.Deck.Hand[^2];var second=p.Deck.Hand[^1];
            p.Deck.TryPutInPlay(first);p.Deck.TryDiscardExact(second);p.Deck.TryRemoveFromDiscardExact(second);p.Deck.PutOnTop(second);
            Play(p,c,b,true);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(old));Assert.That(p.Deck.DrawPileCount,Is.Zero);
            Assert.That(p.Deck.PlayedCards.Count,Is.Zero);Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(3));
        }

        [Test] public void AlreadyHealedNewWoundDoesNotRemoveAnOldSameIdWound()
        {
            var(p,c,b)=Fixture();var old=new DeedCard("w",CardType.Wound);p.Deck.Hand.Add(old);
            p.Wounds++;var fresh=p.Deck.Hand.Last();p.Deck.TryRemovePermanentlyExact(fresh);
            Play(p,c,b,true);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Deck.Hand.Single(),Is.SameAs(old));Assert.That(p.Deck.RemovedCards.Count,Is.EqualTo(2));
        }

        [Test] public void ChoiceCanDeclineCleanupAndPermissionExpiresAtTurnEnd()
        {
            var(p,c,b)=Fixture();Assert.Throws<InvalidOperationException>(()=>BannerActions.ChooseProtectionCleanup(c,false));
            Play(p,c,b,true);p.Wounds++;BannerActions.ChooseProtectionCleanup(c,false);
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);Assert.That(p.Wounds,Is.EqualTo(1));
            p.Wounds++;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);Assert.That(p.Wounds,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>BannerActions.ChooseProtectionCleanup(c,true));
        }

        [Test] public void CleanupDoesNotPreventActualKnockoutOrDiscardOfUnplayedArtifact()
        {
            var(p,c,b)=Fixture();p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));c.InBattle=true;
            Play(p,c,b,true);
            BattleResolver.Resolve(p,new[]{Enemy("monster_036")},Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(c.IsKnockedOut,Is.True);Assert.That(p.Wounds,Is.EqualTo(5));
            Assert.That(p.Deck.DiscardPile.Single().Id,Is.EqualTo("basic_card_000"));
            UnitDamageResolver.EndBattle(p,c);new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(p.Wounds,Is.Zero);Assert.That(p.Deck.DiscardPile.Count,Is.EqualTo(1));
        }
    }
}
