using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ThugsStateTests
    {
        [SetUp] public void Setup()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private static (PlayerState p,ActionContext c,UnitState u) Create(int influence=2)
        {
            var p=new PlayerState();var u=new UnitState(CardJsonLoader.LoadUnit("army_2_011"));p.Units.Add(u);
            return(p,new ActionContext{InBattle=true,InfluencePool=influence},u);
        }
        [Test] public void OriginalThugHasAllPrintedTraits()
        {
            var(p,c,u)=Create();Assert.That(u.Card.Level,Is.EqualTo(1));Assert.That(u.Card.Armor,Is.EqualTo(5));
            Assert.That(u.Card.InfluenceCost,Is.EqualTo(5));Assert.That(u.Card.RecruitLocationMask,Is.EqualTo(RecruitLocation.Village|RecruitLocation.Keep));
            Assert.That(u.Card.DamageInfluenceCost,Is.EqualTo(2));Assert.That(u.Card.ReverseRecruitReputation,Is.True);
            Assert.That(RecruitmentService.GetCost(p,u.Card,RecruitLocation.Village,c),Is.EqualTo(5));
        }
        [TestCase(0,0)] [TestCase(1,-1)] [TestCase(2,-1)]
        public void OriginalAbilitiesApplyReputationOnlyAfterLegalActivation(int index,int reputation)
        {
            var(p,c,u)=Create(0);CardUnitActions.Use(p,c,u,index);
            Assert.That(p.Reputation,Is.EqualTo(reputation));Assert.That(u.IsReady,Is.False);
            Assert.That(c.BlockPool,Is.EqualTo(index==0?3:0));Assert.That(c.MeleePool,Is.EqualTo(index==1?3:0));
            Assert.That(c.InfluencePool,Is.EqualTo(index==2?4:0));
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(p,c,u,index));Assert.That(p.Reputation,Is.EqualTo(reputation));
        }
        [Test] public void ReputationCannotDropBelowPrintedTrack()
        {var(p,c,u)=Create();p.Reputation=-7;CardUnitActions.Use(p,c,u,1);Assert.That(p.Reputation,Is.EqualTo(-7));}
        [TestCase(1,false,0,0)] [TestCase(2,false,1,1)] [TestCase(1,true,0,0)]
        public void AssignmentPaysOnlyWhenDamageActuallyReachesThugs(int influence,bool blocked,int wounds,int hero)
        {
            var(p,c,u)=Create(influence);var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            var blocks=blocked?new[]{new BlockAllocation(0,6,Element.Physical)}:Array.Empty<BlockAllocation>();
            void Act()=>BattleResolver.Resolve(p,new[]{enemy},blocks,Array.Empty<AttackAllocation>(),ctx:c,damageAssignments:new[]{new UnitDamageAllocation(0,new[]{u})});
            if(influence<2&&!blocked)Assert.Throws<InvalidOperationException>(Act);else Act();
            Assert.That(u.Wounds,Is.EqualTo(wounds));Assert.That(p.Wounds,Is.EqualTo(hero));
            Assert.That(c.InfluencePool,Is.EqualTo(influence-(wounds>0?2:0)));Assert.That(c.DamageAssignedUnits.Count,Is.EqualTo(wounds>0?1:0));
        }
        [Test] public void TwoThugsRequireWholePaymentBeforeEitherIsWounded()
        {
            var(p,c,u)=Create(3);var second=new UnitState(u.Card);p.Units.Add(second);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            Assert.Throws<InvalidOperationException>(()=>BattleResolver.Resolve(p,new[]{enemy},Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c,
                damageAssignments:new[]{new UnitDamageAllocation(0,new[]{u,second})}));
            Assert.That(c.InfluencePool,Is.EqualTo(3));Assert.That(u.Wounds+second.Wounds+p.Wounds,Is.Zero);Assert.That(c.DamageAssignedUnits,Is.Empty);
        }
        [Test] public void PreviousUnitAbsorptionDoesNotChargeUnreachedThug()
        {
            var(p,c,u)=Create(0);var other=new UnitState(CardJsonLoader.LoadUnit("army_003"));p.Units.Add(other);
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_027");
            BattleResolver.Resolve(p,new[]{enemy},Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c,
                damageAssignments:new[]{new UnitDamageAllocation(0,new[]{other,u})});
            Assert.That(other.Wounds,Is.EqualTo(1));Assert.That(u.Wounds,Is.Zero);Assert.That(c.InfluencePool,Is.Zero);
        }
    }
}
