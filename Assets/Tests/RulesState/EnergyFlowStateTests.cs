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
    public sealed class EnergyFlowStateTests
    {
        [SetUp]
        public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static UnitState Unit(PlayerState p,string id="army_2_001",bool ready=true,int wounds=0)
        {
            var u=new UnitState(CardJsonLoader.LoadUnit(id));if(!ready)u.Exhaust();u.AddWounds(wounds);p.Units.Add(u);return u;
        }
        private static (PlayerState p,ActionContext c,UnitState u) Setup(int wounds=0)
        {
            var p=new PlayerState{Wounds=3};p.Mana.AddToken(ManaColor.Green);p.Mana.AddToken(ManaColor.Black);
            var u=Unit(p,"army_003",false,wounds);return(p,new ActionContext{DayPart=DayPart.Night,TargetUnit=u},u);
        }
        private static DeedCard Play(PlayerState p,ActionContext c,bool strong,string id="magic_000",int option=0)
        {
            var s=CardJsonLoader.LoadSpells().Single(s=>s.Id==id);var card=new DeedCard(id,CardType.Spell);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost),strong,option);
            return card;
        }

        [TestCase(false)] [TestCase(true)]
        public void OptionalExhaustionCanBeDeclinedWithoutSelectingFirstEnemy(bool strong)
        {
            var(p,c,u)=Setup(1);var other=new PlayerState();var enemy=Unit(other);c.OtherPlayers.Add(other);
            Play(p,c,strong);Assert.That(u.IsReady,Is.True);Assert.That(u.Wounds,Is.EqualTo(strong?0:1));
            Assert.That(enemy.IsReady,Is.True);Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(c.Healing.Total,Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black),Is.EqualTo(strong?0:1));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExplicitSecondUnitFromEachOtherPlayerIsExhausted(bool strong)
        {
            var(p,c,u)=Setup();var firsts=new List<UnitState>();var seconds=new List<UnitState>();
            for(int i=0;i<2;i++)
            {
                var other=new PlayerState();c.OtherPlayers.Add(other);firsts.Add(Unit(other));
                seconds.Add(Unit(other,strong?"army_003":"army_2_001"));c.EnergyExhaustTargets.Add(seconds.Last());
            }
            Play(p,c,strong);Assert.That(u.IsReady,Is.True);
            Assert.That(firsts.All(x=>x.IsReady),Is.True);Assert.That(seconds.All(x=>!x.IsReady),Is.True);
        }

        [TestCase("missing")] [TestCase("foreign")] [TestCase("destroyed")] [TestCase("alreadyReady")]
        public void NoActualOwnedReadyingRejectsBeforeAnyEffect(string problem)
        {
            var(p,c,u)=Setup(1);var other=new PlayerState();var enemy=Unit(other);c.OtherPlayers.Add(other);c.EnergyExhaustTargets.Add(enemy);
            if(problem=="missing")c.TargetUnit=null;if(problem=="foreign")p.Units.Remove(u);
            if(problem=="destroyed")u.Destroy();if(problem=="alreadyReady")u.Ready();
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,true));Assert.That(u.Wounds,Is.EqualTo(1));Assert.That(enemy.IsReady,Is.True);
            Assert.That(p.Mana.Tokens[ManaColor.Green]+p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [TestCase("twoSameOwner")] [TestCase("duplicate")] [TestCase("overlevel")] [TestCase("unlistedOwner")]
        [TestCase("destroyed")] [TestCase("ownUnit")] [TestCase("alreadySpent")]
        public void IllegalOptionalSelectionRejectsWholeCast(string problem)
        {
            var(p,c,u)=Setup(1);var other=new PlayerState();c.OtherPlayers.Add(other);
            var a=Unit(other);var b=Unit(other,problem=="overlevel"?"army_003":"army_2_001");c.EnergyExhaustTargets.Add(b);
            if(problem=="twoSameOwner")c.EnergyExhaustTargets.Insert(0,a);
            if(problem=="duplicate")c.EnergyExhaustTargets.Add(b);
            if(problem=="unlistedOwner")c.OtherPlayers.Clear();
            if(problem=="destroyed")b.Destroy();if(problem=="alreadySpent")b.Exhaust();
            if(problem=="ownUnit"){c.EnergyExhaustTargets.Clear();c.EnergyExhaustTargets.Add(Unit(p));}
            Assert.Throws<InvalidOperationException>(()=>Play(p,c,problem!="overlevel"));
            Assert.That(u.IsReady,Is.False);Assert.That(u.Wounds,Is.EqualTo(1));Assert.That(a.IsReady,Is.True);
            Assert.That(p.Mana.Tokens[ManaColor.Green]+p.Mana.Tokens[ManaColor.Black],Is.EqualTo(2));Assert.That(p.Deck.PlayedCards,Is.Empty);
        }

        [Test]
        public void StrongHealingRemovesAllUnitWoundsAndCureCanSeeActualHistory()
        {
            var(p,c,u)=Setup(2);c.DrawPerHeal=1;var top=new DeedCard("basic_card_000",CardType.Action);p.Deck.PutUnder(top);
            Play(p,c,true);Assert.That(u.Wounds,Is.Zero);Assert.That(c.HealedUnitsThisTurn.Contains(u),Is.True);
            Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(top));Assert.That(p.Wounds,Is.EqualTo(3));
            c.InBattle=true;CardUnitActions.Use(p,c,u,0);
            var enemy=new Monster("actual attack target",5,0,Element.Physical,3,Array.Empty<Ability>());
            var attack=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);
            Assert.That(attack.Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(3));Assert.That(c.MeleePool,Is.Zero);
            UnitDamageResolver.EndBattle(p,c);p.Mana.AddToken(ManaColor.Green);Play(p,c,false,"magic_001");
            Assert.That(u.IsReady,Is.True);Assert.That(p.Deck.DrawPile.Single(),Is.SameAs(top));
        }

        [TestCase("green")] [TestCase("black")] [TestCase("day")] [TestCase("battle")] [TestCase("battleBase")]
        public void ManaAndTimingFailuresCannotMutateUnits(string problem)
        {
            var(p,c,u)=Setup(1);if(problem=="day")c.DayPart=DayPart.Day;else if(problem.StartsWith("battle"))c.InBattle=true;
            else p.Mana.Tokens.Remove(problem=="green"?ManaColor.Green:ManaColor.Black);
            int before=p.Mana.Tokens.Values.Sum();Assert.Throws<InvalidOperationException>(()=>Play(p,c,problem!="battleBase"));
            Assert.That(u.IsReady,Is.False);Assert.That(u.Wounds,Is.EqualTo(1));Assert.That(p.Mana.Tokens.Values.Sum(),Is.EqualTo(before));
        }

        [Test]
        public void SpentEnemyCannotActivateAndOriginalSpellDiscardsAtTurnEnd()
        {
            var(p,c,u)=Setup();var other=new PlayerState();var enemy=Unit(other);c.OtherPlayers.Add(other);c.EnergyExhaustTargets.Add(enemy);
            var source=Play(p,c,true);var enemyContext=new ActionContext{InBattle=true};
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(other,enemyContext,enemy,0));
            Assert.That(enemyContext.MeleePool,Is.Zero);c.SkipDraw=true;
            new PlayerTurnEngine().EndTurn(p,DayPart.Night,new ManaSource(1,()=>DayPart.Night),c);
            Assert.That(p.Deck.DiscardPile.Contains(source),Is.True);Assert.That(enemy.IsReady,Is.False);Assert.That(c.HealedUnitsThisTurn,Is.Empty);
        }

        [TestCase(-1)] [TestCase(1)]
        public void InvalidOptionRejectedBeforePayment(int option)
        {
            var(p,c,u)=Setup();Assert.Throws<InvalidOperationException>(()=>Play(p,c,true,option:option));
            Assert.That(u.IsReady,Is.False);Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
        }
    }
}
