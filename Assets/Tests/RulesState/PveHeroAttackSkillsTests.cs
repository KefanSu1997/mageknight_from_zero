using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveHeroAttackSkillsTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static SkillCard Own(PlayerState player, string pool, int slot)
        {
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_" + pool)[slot]; player.Skills.Add(skill); return skill;
        }
        private static Monster Enemy(int power = 3, Element element = Element.Physical, params Ability[] abilities)
            => new Monster("same", 4, power, element, 2, abilities,
                Attacks: new[] { new EnemyAttack(power, element), new EnemyAttack(power, element) });
        private static ActionContext Context(Monster enemy, Phase phase = Phase.AssignDamage)
            => new ActionContext { InBattle = true, CombatPhase = phase, Enemies = new[] { enemy }, SelectedEnemy = enemy };
        private static BattleResult Damage(PlayerState p, ActionContext c, params UnitDamageAllocation[] units)
            => BattleResolver.Resolve(p, c.Enemies, Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: c,
                damageAssignments: units);
        [TestCase(Element.Fire,0)] [TestCase(Element.Ice,0)] [TestCase(Element.Physical,1)] [TestCase(Element.ColdFire,1)]
        public void ElementalResistConsumesWholeAllowanceOnFirstActualHeroHit(Element element, int option)
        {
            var p = new PlayerState { Armor = 2 }; var c = Context(Enemy(1, element)); var skill = Own(p,"001",0);
            SkillActions.Use(p,c,skill,option); var result = Damage(p,c);
            Assert.That(result.TotalWounds,Is.EqualTo(1)); Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(c.IgnoreFireIceDamage + c.IgnoreOtherDamage,Is.Zero);
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p,c,skill,option));
        }
        [Test] public void ElementalResistKeepsPermissionUntilMatchingHeroDamageAfterUnitAllocation()
        {
            var p = new PlayerState { Armor = 2 }; var unit = new UnitState(CardJsonLoader.LoadUnit("army_003")); p.Units.Add(unit);
            var enemy = Enemy(1,Element.Fire); var c = Context(enemy);
            SkillActions.Use(p,c,Own(p,"001",0),0);
            var result = Damage(p,c,new UnitDamageAllocation(0,new[] { unit },0));
            Assert.That(unit.Wounds,Is.EqualTo(1)); Assert.That(result.TotalWounds,Is.Zero);
            Assert.That(c.IgnoreFireIceDamage,Is.Zero,"unit absorption must not consume hero permission; next actual hero hit consumes it");
        }
        [TestCase(Element.Physical,0,3)] [TestCase(Element.Fire,1,4)] [TestCase(Element.ColdFire,1,4)]
        public void HardenedOnlySelectedSecondAttackUsesActualElement(Element element,int option,int expectedWounds)
        {
            var p = new PlayerState { Armor = 2 }; var c = Context(Enemy(4,element)); c.SelectedEnemyAttackIndex=1;
            SkillActions.Use(p,c,Own(p,"003",1),option);
            Assert.That(Damage(p,c).TotalWounds,Is.EqualTo(expectedWounds));
            Assert.That(c.IgnorePhysicalDamage+c.IgnoreNonPhysicalDamage,Is.Zero);
        }
        [Test] public void HardenedSmallHitDoesNotLeakToNextSlot()
        {
            var p = new PlayerState { Armor = 2 }; var c=Context(Enemy(1));
            SkillActions.Use(p,c,Own(p,"003",1),0);Assert.That(Damage(p,c).TotalWounds,Is.EqualTo(1));
            Assert.That(c.IgnorePhysicalDamage,Is.Zero);
        }
        [TestCase("003",8,0,-1)] [TestCase("004",6,0,-1)] [TestCase("004",6,1,2)]
        public void CurseAndTauntChangeOnlySelectedActualSlot(string pool,int slot,int option,int delta)
        {
            var enemy=Enemy(); var copy=enemy with { }; var p=new PlayerState(); var c=Context(enemy,Phase.Block);
            c.Enemies=new[]{enemy,copy};c.SelectedEnemyAttackIndex=1;
            SkillActions.Use(p,c,Own(p,pool,slot),option);
            Assert.That(CardCombatActions.DefenseProfile(enemy,0,c,0).Attack,Is.EqualTo(3));
            Assert.That(CardCombatActions.DefenseProfile(enemy,0,c,1).Attack,Is.EqualTo(3+delta));
            Assert.That(CardCombatActions.DefenseProfile(copy,1,c,1).Attack,Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveArmor(enemy,0,c),Is.EqualTo(option==1?2:4));
            Assert.That(CombatMath.EffectiveArmor(copy,1,c),Is.EqualTo(4));
            c.Enemies=new[]{copy,enemy};
            Assert.That(CardCombatActions.DefenseProfile(enemy,1,c,1).Attack,Is.EqualTo(3+delta),"identity survives reindex");
        }
        [TestCase(false)] [TestCase(true)]
        public void CurseRejectsFortifiedInRangedThenAllowsArmorInMelee(bool site)
        {
            var p=new PlayerState();var e=Enemy(3,Element.Physical,site?Array.Empty<Ability>():new[]{Ability.Fortified});
            var c=Context(e,Phase.Ranged);c.FortifiedSite=site;var skill=Own(p,"003",8);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,1));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            c.CombatPhase=Phase.Melee;SkillActions.Use(p,c,skill,1);Assert.That(CombatMath.EffectiveArmor(e,0,c),Is.EqualTo(3));
            c.MeleePool=3;Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Melee).Kills,Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidTauntPreservesSkillAndArmor(int invalid)
        {
            var p=new PlayerState();var e=Enemy();var c=Context(e,Phase.Block);var skill=Own(p,"004",6);
            if(invalid==0)c.CombatPhase=Phase.Melee;
            if(invalid==1)c.CancelEnemyAttack(0,0);
            if(invalid==2)c.SelectedEnemy=e with { };
            if(invalid==3)c.SelectedEnemyAttackIndex=9;
            if(invalid==4)c.SuccessfullyBlockedAttacks.Add((0,0));
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,1));
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);Assert.That(CombatMath.EffectiveArmor(e,0,c),Is.EqualTo(4));
        }
        [TestCase(false)] [TestCase(true)]
        public void TauntArmorRealConsumerAndLaterStop(bool stop)
        {
            var p=new PlayerState();var e=Enemy();var c=Context(e,Phase.Block);SkillActions.Use(p,c,Own(p,"004",6),1);
            if(stop)c.CancelEnemyAttack(0,0);
            Assert.That(CombatMath.EffectiveArmor(e,0,c),Is.EqualTo(stop?4:2));
            c.CombatPhase=Phase.Melee;c.MeleePool=2;
            Assert.That(CardCombatActions.Attack(p,c,c.Enemies,new[]{0},Phase.Melee).Kills,Is.EqualTo(stop?0:1));
        }
        [TestCase("003",8)] [TestCase("003",1)]
        public void SessionEntryRestoresTransientEnemyAndAttackAfterRealEffect(string pool,int slot)
        {
            var p=new PlayerState();var e=Enemy(4);var skill=Own(p,pool,slot);
            var session=new MK.Logic.Runtime.Battles.PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions());
            session.Advance(); if(slot==1) session.Advance();
            var old=e with {};session.Context.SelectedEnemy=old;session.Context.SelectedEnemyAttackIndex=9;
            session.UseSkill(skill,0,target:0,attackIndex:1);
            Assert.That(ReferenceEquals(session.Context.SelectedEnemy,old),Is.True);
            Assert.That(session.Context.SelectedEnemyAttackIndex,Is.EqualTo(9));
            Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            if(slot==8) Assert.That(CardCombatActions.DefenseProfile(e,0,session.Context,1).Attack,Is.EqualTo(3));
            else { session.Advance(); Assert.That(p.Wounds,Is.EqualTo(3)); }
        }
        [Test] public void BothDefensePermissionsAndSlotModifiersClearAtRealBattleEnd()
        {
            var p=new PlayerState();var e=Enemy();var c=Context(e);SkillActions.Use(p,c,Own(p,"001",0),0);
            SkillActions.Use(p,c,Own(p,"003",1),0);c.CombatPhase=Phase.Block;SkillActions.Use(p,c,Own(p,"004",6),1);
            UnitDamageResolver.EndBattle(p,c);
            Assert.That(c.IgnorePhysicalDamage+c.IgnoreNonPhysicalDamage+c.IgnoreFireIceDamage+c.IgnoreOtherDamage,Is.Zero);
            Assert.That(CardCombatActions.DefenseProfile(e,0,c).Attack,Is.EqualTo(3));Assert.That(CombatMath.EffectiveArmor(e,0,c),Is.EqualTo(4));
        }
    }
}
