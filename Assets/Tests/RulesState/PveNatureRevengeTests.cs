using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveNatureRevengeTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private (PlayerState p,ActionContext c,Monster enemy) Create()
        {
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var p=new PlayerState{HeroId="Braevalar",Armor=2};
            var c=new ActionContext{InBattle=true,CombatPhase=Phase.Block,Enemies=new[]{enemy}};
            return(p,c,enemy);
        }
        private void Move(PlayerState p,ActionContext c)
        {
            var card=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(card);
            CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id));
            Assert.That(p.Deck.PlayedCards.Contains(card),Is.True);
        }
        [Test] public void PrintedMarchInCombatPaysOneForOneAndRealDamageUsesRemainingAttack()
        {
            var(p,c,e)=Create();NatureRevengeActions.Grant(c,e);Move(p,c);
            NatureRevengeActions.SpendMove(c,e,0,2);
            Assert.That(c.MovementPool,Is.Zero);Assert.That(CardCombatActions.DefenseProfile(e,0,c).Attack,Is.EqualTo(e.Attack-3));
            c.CombatPhase=Phase.AssignDamage;
            var result=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
            Assert.That(result.TotalWounds,Is.EqualTo(1));Assert.That(p.Wounds,Is.EqualTo(1));
        }
        [Test] public void SameNamedEnemyAndOtherAttackSlotRetainTheirPrintedPower()
        {
            var(p,c,e)=Create();e=e with{Attacks=new[]{new EnemyAttack(e.Attack,Element.Physical),new EnemyAttack(3,Element.Fire)}};
            var other=e with{};c.Enemies=new[]{e,other};NatureRevengeActions.Grant(c,e,1);Move(p,c);
            NatureRevengeActions.SpendMove(c,e,1,1);
            Assert.That(CardCombatActions.DefenseProfile(e,0,c,1).Attack,Is.EqualTo(1));
            Assert.That(CardCombatActions.DefenseProfile(e,0,c,0).Attack,Is.EqualTo(e.Attack));
            Assert.That(CardCombatActions.DefenseProfile(other,1,c,1).Attack,Is.EqualTo(3));
            Assert.That(NatureRevengeActions.HasCumbersome(c,other),Is.False);
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(c,other,1,1));Assert.That(c.MovementPool,Is.EqualTo(1));
        }
        [Test] public void CumbersomeAppliesToWholeEnemyButEachSpendTargetsOneOfItsAttacks()
        {
            var(p,c,e)=Create();e=e with{Attacks=new[]{new EnemyAttack(e.Attack,Element.Physical),new EnemyAttack(3,Element.Fire)}};c.Enemies=new[]{e};
            NatureRevengeActions.Grant(c,e,0);Move(p,c);NatureRevengeActions.SpendMove(c,e,1,2);
            Assert.That(CardCombatActions.DefenseProfile(e,0,c,0).Attack,Is.EqualTo(e.Attack-1));
            Assert.That(CardCombatActions.DefenseProfile(e,0,c,1).Attack,Is.EqualTo(1));
        }
        [Test] public void ReducedToZeroIsActualSuccessfulBlockWithoutConsumingOtherBlock()
        {
            var(p,c,e)=Create();NatureRevengeActions.Grant(c,e);Move(p,c);Move(p,c);c.CombatPower.AddBlock(2);
            NatureRevengeActions.SpendMove(c,e,0,e.Attack-1);
            Assert.That(c.SuccessfullyBlockedAttacks.Contains((0,0)),Is.True);Assert.That(c.WasBlockedThisBattle(e),Is.True);
            Assert.That(c.CombatPower.BlockTotal,Is.EqualTo(2));Assert.That(c.MovementPool,Is.EqualTo(5-e.Attack));
            c.CombatPhase=Phase.AssignDamage;
            Assert.That(BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c).TotalWounds,Is.Zero);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(3)]
        public void InvalidPaymentIsAtomic(int points)
        {
            var(p,c,e)=Create();NatureRevengeActions.Grant(c,e);Move(p,c);
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(c,e,0,points));
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(NatureRevengeActions.Reduction(c,e,0),Is.EqualTo(1));
        }
        [TestCase(Phase.Ranged)] [TestCase(Phase.AssignDamage)] [TestCase(Phase.Melee)]
        public void SpendingOutsideBlockPreservesMoveAndReduction(Phase phase)
        {
            var(p,c,e)=Create();NatureRevengeActions.Grant(c,e);Move(p,c);c.CombatPhase=phase;
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(c,e,0,1));
            Assert.That(c.MovementPool,Is.EqualTo(2));Assert.That(NatureRevengeActions.Reduction(c,e,0),Is.EqualTo(1));
        }
        [Test] public void DeadForeignCancelledOrInvalidAttackCannotGrantState()
        {
            var(p,c,e)=Create();Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.Grant(c,e with{}));
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.Grant(c,e,1));c.CancelEnemyAttack(0,0);
            Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.Grant(c,e));Assert.That(c.NatureRevenge.CumbersomeTargets,Is.Empty);
        }
        [Test] public void BattleClearKeepsThisTurnGrantAndTurnClearRestoresOriginalPower()
        {
            var(p,c,e)=Create();NatureRevengeActions.Grant(c,e);Move(p,c);NatureRevengeActions.SpendMove(c,e,0,1);
            UnitDamageResolver.EndBattle(p,c);
            var game=new GameState();game.Players.Add(p);new TurnMachine(game).BeginBattle(c);
            Assert.That(c.Enemies[0],Is.SameAs(e));
            Assert.That(NatureRevengeActions.HasCumbersome(c,e),Is.True);
            Assert.That(NatureRevengeActions.Reduction(c,e,0),Is.EqualTo(1));
            Assert.That(CardCombatActions.DefenseProfile(e,0,c).Attack,Is.EqualTo(e.Attack-1));
            UnitDamageResolver.EndBattle(p,c);c.SkipDraw=true;
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,c);
            Assert.That(NatureRevengeActions.HasCumbersome(c,e),Is.False);Assert.That(CardCombatActions.DefenseProfile(e,0,c).Attack,Is.EqualTo(e.Attack));
            Assert.That(c.MovementPool,Is.Zero);Assert.Throws<InvalidOperationException>(()=>NatureRevengeActions.SpendMove(c,e,0,1));
        }
    }
}
