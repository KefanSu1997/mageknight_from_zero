using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveForkedLightningTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static Monster Original(string id="monster_032")=>CardJsonLoader.LoadMonsters().Single(m=>m.Id==id);
  static (PlayerState p,ActionContext c,SkillCard s) Setup(params Monster[] enemies)
  {var p=new PlayerState();var c=new ActionContext{InBattle=true,CombatPhase=Phase.Ranged,Enemies=enemies};var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[5];HeroSkillAcquisition.Acquire(p,s);return(p,c,s);}
  static void Fireball(PlayerState p,ActionContext c)
  {var original=CardJsonLoader.LoadSpells().Single(x=>x.Id=="magic_009");var a=new DeedCard(original.Id,CardType.Spell);p.Deck.Hand.Add(a);p.Mana.AddToken(ManaColor.Red);var data=new ActionCardData(original.Id,original.Name,original.Set,original.ImagePath,original.EnImagePath,original.Top.Effect,original.Bottom.Effect,original.Top.ManaCost);CardPlayActions.Play(p,c,a,data,actionSystem:new ActionSystem(false,new[]{original}));}
  [Test] public void ActualThreeTargetsGetSeparateColdFirePartsAndCannotPoolAgainstOne()
  {var enemies=new[]{Original(),Original(),Original()};var(p,c,s)=Setup(enemies);ForkedLightningActions.Use(p,c,s,enemies);Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(3));for(int i=0;i<3;i++){var a=c.CombatPower.Attacks[i];Assert.That(a.RequiredEnemy,Is.SameAs(enemies[i]));Assert.That(a.SourceSkill,Is.SameAs(s));Assert.That(a.SourceCard,Is.Null);Assert.That(a.Type,Is.EqualTo(AttackType.Ranged));Assert.That(a.Element,Is.EqualTo(Element.ColdFire));Assert.That(a.Value,Is.EqualTo(1));}var r=CardCombatActions.Attack(p,c,enemies,new[]{0},Phase.Ranged);Assert.That(r.PrintedPower,Is.EqualTo(1));Assert.That(r.Kills,Is.Zero);Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(2));Assert.That(c.CombatPower.Attacks[0].RequiredEnemy,Is.SameAs(enemies[1]));Fireball(p,c);var next=CardCombatActions.Attack(p,c,enemies,new[]{1},Phase.Ranged);Assert.That(next.Kills,Is.EqualTo(1));Assert.That(p.Fame,Is.EqualTo(enemies[1].Fame));Assert.That(c.CombatPower.Attacks.Single().RequiredEnemy,Is.SameAs(enemies[2]));}
  [Test] public void SameValueMonsterInstancesDoNotStealEachOthersContributionWhenReordered()
  {var a=Original();var b=Original();var(p,c,s)=Setup(a,b);ForkedLightningActions.Use(p,c,s,new[]{a,b});var reordered=new[]{b,a};c.Enemies=reordered;CardCombatActions.Attack(p,c,reordered,new[]{0},Phase.Ranged);Assert.That(c.CombatPower.Attacks.Single().RequiredEnemy,Is.SameAs(a));}
  [TestCase(false)] [TestCase(true)] public void GroupAttackUsesOnlyBoundEnemyCapacityAndGenericRemainder(bool generic)
  {var a=Original("monster_057");var b=Original("monster_057");Assert.That(a.Armor,Is.EqualTo(1));var enemies=new[]{a,b};var(p,c,s)=Setup(enemies);ForkedLightningActions.Use(p,c,s,new[]{a});c.DoubleRangedAttack=true;if(generic)Fireball(p,c);c.EnemyDefense.Choose(enemies,new[]{0,1},c,0,0);var r=CardCombatActions.Attack(p,c,enemies,new[]{0,1},Phase.Ranged);Assert.That(r.Kills,Is.EqualTo(generic?2:0));if(!generic)Assert.That(r.EffectivePower,Is.EqualTo(2));Assert.That(r.RequiredPower,Is.EqualTo(6));}
  [Test] public void ThreeEnemyGroupUsesOriginalElusiveAndDefendArmorForActualFireAttack()
  {var enemies=new[]{Original(),Original(),Original("monster_057")};var(p,c,s)=Setup(enemies);ForkedLightningActions.Use(p,c,s,enemies);Fireball(p,c);c.EnemyDefense.Choose(enemies,new[]{0,1,2},c,2,2);var r=CardCombatActions.Attack(p,c,enemies,new[]{0,1,2},Phase.Ranged);Assert.That(r.PrintedPower,Is.EqualTo(8));Assert.That(r.RequiredPower,Is.EqualTo(9));Assert.That(r.Kills,Is.Zero);Assert.That(p.Fame,Is.Zero);}
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] public void InvalidTargetsDoNotConsumeSkillOrCreateParts(int mode)
  {var enemies=new[]{Original(),Original(),Original(),Original()};var(p,c,s)=Setup(enemies);Monster[] targets=mode==0?Array.Empty<Monster>():mode==1?new[]{enemies[0],enemies[0]}:mode==2?enemies:new[]{Original()};Assert.Throws<InvalidOperationException>(()=>ForkedLightningActions.Use(p,c,s,targets));Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void DeadTargetAndWrongPhaseRejectBeforeUse()
  {var enemy=Original();var(p,c,s)=Setup(enemy);CardCombatActions.DestroyEnemy(p,c,0);Assert.Throws<InvalidOperationException>(()=>ForkedLightningActions.Use(p,c,s,new[]{enemy}));Assert.That(SkillActions.IsUsed(p,s),Is.False);var living=Original();c.Enemies=new[]{living};c.DefeatedEnemyIndices.Clear();c.CombatPhase=Phase.Block;Assert.Throws<InvalidOperationException>(()=>ForkedLightningActions.Use(p,c,s,new[]{living}));Assert.That(c.CombatPower.Attacks,Is.Empty);}
  [Test] public void FortificationRejectsRangedButSameActualPartWorksInAttackPhase()
  {var enemy=Original("monster_033");var(p,c,s)=Setup(enemy);ForkedLightningActions.Use(p,c,s,new[]{enemy});Assert.Throws<InvalidOperationException>(()=>CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Ranged));Assert.That(c.CombatPower.Attacks.Count,Is.EqualTo(1));c.CombatPhase=Phase.Melee;var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);Assert.That(r.EffectivePower,Is.Zero);Assert.That(c.CombatPower.Attacks,Is.Empty);}
  [Test] public void ResistedHalvesJoinOnlyAtWholeAttack()
  {var enemy=Original("monster_033");var(p,c,s)=Setup(enemy);c.CombatPhase=Phase.Melee;ForkedLightningActions.Use(p,c,s,new[]{enemy});c.CombatPower.AddAttack(1,Element.Physical);var r=CardCombatActions.Attack(p,c,new[]{enemy},new[]{0},Phase.Melee);Assert.That(r.EffectivePower,Is.EqualTo(1));}
  [Test] public void FormalEndTurnDropsPartsAndRestoresSkillForNextTurn()
  {var enemy=Original();var(p,c,s)=Setup(enemy);new PlayerTurnEngine().StartTurn(p,DayPart.Day);ForkedLightningActions.Use(p,c,s,new[]{enemy});Assert.Throws<InvalidOperationException>(()=>ForkedLightningActions.Use(p,c,s,new[]{enemy}));UnitDamageResolver.EndBattle(p,c);new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);new PlayerTurnEngine().StartTurn(p,DayPart.Day);Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
 }
}
