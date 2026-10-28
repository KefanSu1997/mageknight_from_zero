using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RestorationStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c) Setup(bool forest=false)
        {
            var p=new PlayerState{Wounds=3};p.Mana.AddToken(ManaColor.Green);p.Mana.AddToken(ManaColor.Black);
            return (p,new ActionContext{DayPart=DayPart.Night,CurrentTerrain=forest?TerrainType.Forest:TerrainType.Plains});
        }
        private static UnitState Unit(PlayerState p,string id,int wounds=0)
        {
            var u=new UnitState(CardJsonLoader.LoadUnit(id));u.Exhaust();u.AddWounds(wounds);p.Units.Add(u);return u;
        }
        private static DeedCard Play(PlayerState p,ActionContext c,bool strong,int option=0)
        {
            var s=CardJsonLoader.LoadSpells().Single(s=>s.Id=="magic_005");var card=new DeedCard(s.Id,CardType.Spell);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,
                s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost),strong,option);return card;
        }

        [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
        public void HealingIsSpendableAndDoesNotRequireUnitTargets(bool strong,bool forest)
        {
            var(p,c)=Setup(forest);c.ReadyUnitTargets=new List<UnitState>();Play(p,c,strong);
            Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(c.Healing.Total,Is.EqualTo(forest?5:3));
            CardHealing.Spend(p,c,3);Assert.That(p.Wounds,Is.Zero);Assert.That(c.Healing.Total,Is.EqualTo(forest?2:0));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.EqualTo(strong?0:1));
        }

        [TestCase(false)] [TestCase(true)]
        public void SumBudgetReadiesMultipleOriginalUnitsAndHealingRemainsIndependent(bool forest)
        {
            var(p,c)=Setup(forest);var a=Unit(p,"army_2_006",1);var b=Unit(p,"army_2_001",1);
            var unselected=Unit(p,"army_003");c.ReadyUnitTargets=new List<UnitState>{a,b};
            if(forest)c.ReadyUnitTargets.Add(Unit(p,"army_2_005"));
            Play(p,c,true);Assert.That(c.ReadyUnitTargets.All(u=>u.IsReady),Is.True);
            Assert.That(unselected.IsReady,Is.False);Assert.That(a.Wounds+b.Wounds,Is.EqualTo(2));
            CardHealing.Spend(p,c,unit:b);CardHealing.Spend(p,c,unit:a);
            Assert.That(a.CanActivate&&b.CanActivate,Is.True);Assert.That(c.Healing.Total,Is.EqualTo(forest?2:0));
            Assert.That(p.Wounds,Is.EqualTo(3));
            c.InBattle=true;CardUnitActions.Use(p,c,a,0);CardUnitActions.Use(p,c,b,0);
            Assert.That(a.IsReady||b.IsReady,Is.False);Assert.That(c.MeleePool,Is.EqualTo(4));Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void BaseNeverReadiesSelectedUnits(bool forest)
        {
            var(p,c)=Setup(forest);var u=Unit(p,"army_2_001");c.TargetUnit=u;c.ReadyUnitTargets=new List<UnitState>{u};
            Play(p,c,false);Assert.That(u.IsReady,Is.False);Assert.That(c.Healing.Total,Is.EqualTo(forest?5:3));
        }

        [TestCase("over3")] [TestCase("over5")] [TestCase("duplicate")] [TestCase("foreign")] [TestCase("destroyed")]
        public void InvalidTargetSetRejectsBeforeAnyPaymentOrReadying(string problem)
        {
            var(p,c)=Setup(problem=="over5");var a=Unit(p,"army_2_001");var b=Unit(p,problem=="over5"?"army_003":"army_2_001");
            c.ReadyUnitTargets=new List<UnitState>{a,b};
            if(problem=="over5")c.ReadyUnitTargets.Add(Unit(p,"army_2_006"));
            if(problem=="duplicate")c.ReadyUnitTargets=new List<UnitState>{a,a};
            if(problem=="foreign"){p.Units.Remove(b);c.ReadyUnitTargets=new List<UnitState>{Unit(p,"army_2_006"),b};}
            if(problem=="destroyed"){b.Destroy();c.ReadyUnitTargets=new List<UnitState>{Unit(p,"army_2_006"),b};}
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true));
            Assert.That(c.ReadyUnitTargets.All(u=>!u.IsReady),Is.True);Assert.That(c.Healing.Total,Is.Zero);
            Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(p.Deck.PlayedCards,Is.Empty);
            Assert.That(p.Mana.Tokens[ManaColor.Green]+p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void CannotCastDuringCombatEvenToOnlyReady(bool strong)
        {
            var(p,c)=Setup();var u=Unit(p,"army_2_001");c.TargetUnit=u;c.InBattle=true;
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,strong));Assert.That(u.IsReady,Is.False);
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(c.Healing.Total,Is.Zero);
        }

        [TestCase("green")] [TestCase("black")] [TestCase("day")]
        public void PrintedManaAndNightRestrictionAreAtomic(string problem)
        {
            var(p,c)=Setup();var u=Unit(p,"army_2_001");c.TargetUnit=u;
            if(problem=="day")c.DayPart=DayPart.Day;
            else p.Mana.Tokens.Remove(problem=="green"?ManaColor.Green:ManaColor.Black);
            var before=p.Mana.Tokens.Values.Sum();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true));Assert.That(u.IsReady,Is.False);
            Assert.That(p.Mana.Tokens.Values.Sum(),Is.EqualTo(before));Assert.That(c.Healing.Total,Is.Zero);
        }

        [Test]
        public void LegacySingleTargetStillWorksAndExplicitEmptyOverridesIt()
        {
            var(p,c)=Setup();var u=Unit(p,"army_003");c.TargetUnit=u;Play(p,c,true);Assert.That(u.IsReady,Is.True);
            var(p2,c2)=Setup();var u2=Unit(p2,"army_003");c2.TargetUnit=u2;c2.ReadyUnitTargets=new List<UnitState>();
            Play(p2,c2,true);Assert.That(u2.IsReady,Is.False);Assert.That(c2.Healing.Total,Is.EqualTo(3));
        }

        [Test]
        public void SourceAndUnusedHealingEndWithTurn()
        {
            var(p,c)=Setup(true);c.SkipDraw=true;var card=Play(p,c,false);
            Assert.That(c.Healing.Total,Is.EqualTo(5));
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,new ManaSource(1,()=>DayPart.Night),c);
            Assert.That(c.Healing.Total,Is.Zero);Assert.That(p.Deck.DiscardPile.Contains(card),Is.True);
        }

        [TestCase(-1)] [TestCase(1)]
        public void NonexistentOptionRejectedBeforeFee(int option)
        {
            var(p,c)=Setup();Assert.Throws<InvalidOperationException>(()=>Play(p,c,true,option));
            Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }
    }
}
