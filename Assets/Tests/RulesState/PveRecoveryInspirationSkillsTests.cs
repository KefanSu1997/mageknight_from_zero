using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveRecoveryInspirationSkillsTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static SkillCard Skill(PlayerState p,string pool,int index){var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_"+pool)[index];p.Skills.Add(s);return s;}
  static PlayerState Player(){var p=new PlayerState{Fame=3,Reputation=5};p.Deck.SetDeck(new[]{new DeedCard("basic_card_000",CardType.Action),new DeedCard("basic_card_008",CardType.Action),new DeedCard("basic_card_014",CardType.Action)});return p;}
  [TestCase("000",ManaColor.Red)] [TestCase("002",ManaColor.White)] [TestCase("005",ManaColor.Green)] [TestCase("007",ManaColor.Blue)]
  public void InspirationDrawsActualCardsAndRewardsStrictlyLowestFame(string pool,ManaColor color){var p=Player();var c=new ActionContext();c.OtherPlayers.Add(new PlayerState{Fame=4,Reputation=-5});var skill=Skill(p,pool,8);SkillActions.Use(p,c,skill);Assert.That(p.Deck.Hand.Select(x=>x.Id),Is.EqualTo(new[]{"basic_card_000","basic_card_008"}));Assert.That(p.Mana.Tokens[color],Is.EqualTo(1));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,skill),Is.True);Assert.That(p.Mana.TryPayExactColors(new[]{color},c,p),Is.True);Assert.That(p.Mana.GetTotal(),Is.Zero);}
  [TestCase(2)] [TestCase(3)] public void TiedOrHigherFameDoesNotReceiveBonus(int otherFame){var p=Player();var c=new ActionContext();c.OtherPlayers.Add(new PlayerState{Fame=otherFame,Reputation=99});SkillActions.Use(p,c,Skill(p,"004",8));Assert.That(p.Fame,Is.EqualTo(3));Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));}
  [Test] public void FameInspirationPaysActualLevelThreshold(){var p=Player();p.Fame=7;var c=new ActionContext();c.OtherPlayers.Add(new PlayerState{Fame=8});SkillActions.Use(p,c,Skill(p,"004",8));Assert.That(p.Fame,Is.EqualTo(8));Assert.That(p.FameLevel,Is.EqualTo(3));}
  [Test] public void OwnTurnLocksOtherInspirationThroughFollowingPersonalTurn(){var p=Player();var c=new ActionContext();var e=new PlayerTurnEngine();var source=new ManaSource(1,()=>DayPart.Day);var first=Skill(p,"000",8);var next=Skill(p,"002",8);e.StartTurn(p,DayPart.Day);SkillActions.Use(p,c,first);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,next));e.EndTurn(p,DayPart.Day,source,c);e.StartTurn(p,DayPart.Day);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,next));e.EndTurn(p,DayPart.Day,source,c,true);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,next));e.DrawAfterEndTurn(p,c);SkillActions.Use(p,c,next);Assert.That(SkillActions.IsUsed(p,next),Is.True);}
  [Test] public void OtherTurnLocksUntilNextPersonalTurnAndFailedChoiceDoesNotSpend(){var p=Player();var c=new ActionContext();var first=Skill(p,"000",8);var next=Skill(p,"007",8);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,first,1));Assert.That(SkillActions.IsUsed(p,first),Is.False);SkillActions.Use(p,c,first);var e=new PlayerTurnEngine();e.StartTurn(p,DayPart.Day);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,next));e.EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);SkillActions.Use(p,c,next);}
  [TestCase(0)] [TestCase(1)] public void RallyUsesActualOwnedUnitAndKeepsOtherState(int option){var p=Player();var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));p.Units.Add(unit);unit.Exhaust();unit.AddWounds(1);var c=new ActionContext{TargetUnit=unit};var skill=Skill(p,"002",2);SkillActions.Use(p,c,skill,option);Assert.That(unit.IsReady,Is.EqualTo(option==0));Assert.That(unit.Wounds,Is.EqualTo(option==0?1:0));Assert.That(SkillActions.IsUsed(p,skill),Is.True);}
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void InvalidRallyDoesNotSpend(int invalid){var p=Player();var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));if(invalid!=0)p.Units.Add(unit);unit.Exhaust();unit.AddWounds(1);var c=new ActionContext{TargetUnit=unit};int option=0;if(invalid==1)c.InBattle=true;if(invalid==2)unit.Ready();if(invalid==3){unit.Heal(1);option=1;}if(invalid==4)option=2;var skill=Skill(p,"002",2);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,option));Assert.That(SkillActions.IsUsed(p,skill),Is.False);}
  [Test] public void RallyHealingFeedsActualCureReadinessAndHistory()
  {
   var p=Player(); var c=new ActionContext(); var unit=new UnitState(CardJsonLoader.LoadUnit("army_000"));
   p.Units.Add(unit); unit.Exhaust(); unit.AddWounds(1); c.TargetUnit=unit;
   new MK.Logic.Runtime.CardEffects.CureEffect(false).Execute(p,c);
   SkillActions.Use(p,c,Skill(p,"002",2),1);
   Assert.That(unit.Wounds,Is.Zero); Assert.That(unit.IsReady,Is.True);
   Assert.That(c.HealedUnitsThisTurn.Contains(unit),Is.True);
  }
 }
}


