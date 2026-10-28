using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveInsightPreyTests
 {
  [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
  static Monster Enemy(params Ability[] abilities) => new Monster("same",4,3,Element.ColdFire,2,abilities,Attacks:new[]{new EnemyAttack(3,Element.ColdFire),new EnemyAttack(3,Element.ColdFire)});
  static ActionContext Context(Monster e) => new ActionContext { InBattle=true, CombatPhase=Phase.Block,Enemies=new[]{e},SelectedEnemy=e,SelectedEnemyAttackIndex=1 };
  static SkillCard Skill(PlayerState p) { var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_004")[5];p.Skills.Add(s);return s; }
  [TestCase(Ability.PhysicalResist,Element.Physical)] [TestCase(Ability.FireResist,Element.Fire)] [TestCase(Ability.IceResist,Element.Ice)] [TestCase(Ability.ColdFireResist,Element.ColdFire)]
  public void ActualResistanceConsumerAndIdentity(Ability a,Element element)
  {
   var e=Enemy(a);var copy=e with {};var c=Context(e);c.Enemies=new[]{copy,e};var p=new PlayerState();SkillActions.Use(p,c,Skill(p),100+(int)a);
   Assert.That(CombatMath.HasResistance(e,1,element,c),Is.False);Assert.That(CombatMath.HasResistance(copy,0,element,c),Is.True);
   var parts=new[]{new AttackProfile(AttackType.Melee,4,element)};
   Assert.That(CombatMath.EffectiveAttack(c.Enemies,new[]{1},parts,Phase.Melee,c),Is.EqualTo(4));
   Assert.That(CombatMath.EffectiveAttack(c.Enemies,new[]{0},parts,Phase.Melee,c),Is.EqualTo(2));
  }
  [Test] public void TokenFortificationRemovedButSiteRemains()
  {
   var e=Enemy(Ability.Fortified);var c=Context(e);c.CombatPhase=Phase.Ranged;var p=new PlayerState();SkillActions.Use(p,c,Skill(p),100+(int)Ability.Fortified);
   var ranged=new[]{new AttackProfile(AttackType.Ranged,4,Element.Physical)};var siege=new[]{new AttackProfile(AttackType.Siege,4,Element.Physical)};
   Assert.That(CombatMath.CanAttack(c.Enemies,new[]{0},ranged,Phase.Ranged,c),Is.True);
   c.FortifiedSite=true;Assert.That(CombatMath.CanAttack(c.Enemies,new[]{0},ranged,Phase.Ranged,c),Is.False);
   Assert.That(CombatMath.CanAttack(c.Enemies,new[]{0},siege,Phase.Ranged,c),Is.True);
  }
  [TestCase(Element.Fire)] [TestCase(Element.Ice)]
  public void ElementOnlyOneActualSlot(Element remaining)
  {
   var e=Enemy();var copy=e with {};var c=Context(e);c.Enemies=new[]{e,copy};var p=new PlayerState();var s=Skill(p);SkillActions.Use(p,c,s,200+(int)remaining);
   Assert.That(CardCombatActions.DefenseProfile(e,0,c,0).AttackElement,Is.EqualTo(Element.ColdFire));
   Assert.That(CardCombatActions.DefenseProfile(e,0,c,1).AttackElement,Is.EqualTo(remaining));
   Assert.That(CardCombatActions.DefenseProfile(copy,1,c,1).AttackElement,Is.EqualTo(Element.ColdFire));
   Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,200+(int)remaining));
   c.ClearAttackReductions();Assert.That(CardCombatActions.DefenseProfile(e,0,c,1).AttackElement,Is.EqualTo(Element.ColdFire));
  }
  [Test] public void BrutalRemovalChangesActualWoundsForBothAttacks()
  {
   var e=Enemy(Ability.Brutal);var c=Context(e);var p=new PlayerState {Armor=2};SkillActions.Use(p,c,Skill(p),100+(int)Ability.Brutal);
   var r=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c);
   Assert.That(r.TotalWounds,Is.EqualTo(4));Assert.That(e.Abilities.Contains(Ability.Brutal),Is.True);
  }
  [Test] public void RemovedSwiftActuallyLowersRequiredBlock()
  {
   var e=Enemy(Ability.Swift);var c=Context(e);var p=new PlayerState();SkillActions.Use(p,c,Skill(p),100+(int)Ability.Swift);
   var blocks=new[]{new BlockAllocation(0,3,Element.ColdFire,AttackIndex:0),new BlockAllocation(0,3,Element.ColdFire,AttackIndex:1)};
   Assert.That(BattleResolver.Resolve(p,c.Enemies,blocks,Array.Empty<AttackAllocation>(),ctx:c).TotalWounds,Is.Zero);
  }
  [Test] public void SelectedColdFireSlotBecomesFireForActualBlocking()
  {
   var e=Enemy();var c=Context(e);var p=new PlayerState {Armor=2};SkillActions.Use(p,c,Skill(p),201);
   var blocks=new[]{new BlockAllocation(0,3,Element.ColdFire,AttackIndex:0),new BlockAllocation(0,3,Element.Ice,AttackIndex:1)};
   Assert.That(BattleResolver.Resolve(p,c.Enemies,blocks,Array.Empty<AttackAllocation>(),ctx:c).TotalWounds,Is.Zero);
  }
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
  public void InvalidChoiceDoesNotSpendSkill(int mode)
  {
   var e=Enemy(mode==0?new[]{Ability.MagicResist}:Array.Empty<Ability>());var c=Context(e);var p=new PlayerState();var s=Skill(p);int option=201;
   if(mode==1)c.SelectedEnemy=e with {};if(mode==2)option=100+(int)Ability.Poison;if(mode==3)c.SelectedEnemyAttackIndex=9;
   Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,option));Assert.That(SkillActions.IsUsed(p,s),Is.False);
  }
 }
}
