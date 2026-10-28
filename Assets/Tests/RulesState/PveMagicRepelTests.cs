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
 public sealed class PveMagicRepelTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static (PlayerState p,ActionContext c,SkillCard skill,DeedCard card) Setup()
  {
   var p=new PlayerState();var c=new ActionContext {ManaSource=new ManaSource(1,()=>DayPart.Day),DayPart=DayPart.Day};
   foreach(var d in c.ManaSource.Dice)d.TurnTo(ManaColor.Red);
   var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[7];p.Skills.Add(s);var card=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(card);return(p,c,s,card);
  }
  static void Use(PlayerState p,ActionContext c,SkillCard s,DeedCard card,bool strong,SidewaysType type=SidewaysType.Attack)=>SkillActions.PlaySideways(p,c,s,card,type,forgoSourceDice:strong);
  [TestCase(SidewaysType.Attack)] [TestCase(SidewaysType.Block)] [TestCase(SidewaysType.Move)] [TestCase(SidewaysType.Influence)]
  public void ExplicitThreeUsesOnlyOneActualCard(SidewaysType type)
  {
   var(p,c,s,card)=Setup();Use(p,c,s,card,true,type);Assert.That(p.ForgoSourceDiceThisTurn,Is.True);Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));
   Assert.That(c.MeleePool+c.BlockPool+c.MovementPool+c.InfluencePool,Is.EqualTo(3));var next=new DeedCard(card.Id,CardType.Action);p.Deck.Hand.Add(next);new ActionSystem(false).PlaySideways(next,p,c,type);
   Assert.That(c.MeleePool+c.BlockPool+c.MovementPool+c.InfluencePool,Is.EqualTo(4));Assert.Throws<InvalidOperationException>(()=>Use(p,c,s,next,true,type));
  }
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
  public void ActualSourceHistoryIncludingExtraAndReturnedDiceRefusesThree(int method)
  {
   var(p,c,s,card)=Setup();var source=c.ManaSource;var die=source.Dice[0];
   switch(method){case 0:source.TakeOrdinary(p,die);source.Return(die);break;case 1:source.TakeExtra(p,die);break;case 2:source.TakeTransformed(p,new[]{die},new[]{ManaColor.Blue});break;case 3:source.ClaimForRound(p,ManaColor.Red);break;case 4:source.Take(p,ManaColor.Red);break;default:source.TakeAny(p);break;}
   Assert.That(source.WasUsedThisTurn(p),Is.True);Assert.Throws<InvalidOperationException>(()=>Use(p,c,s,card,true));Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(p.Deck.Hand.Single(),Is.SameAs(card));Use(p,c,s,card,false);Assert.That(c.MeleePool,Is.EqualTo(2));
  }
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
  public void ThreeBlocksEveryActualSourceTakeWithoutMutation(int method)
  {
   var(p,c,s,card)=Setup();Use(p,c,s,card,true);var source=c.ManaSource;var die=source.Dice[0];
   Assert.Throws<InvalidOperationException>(()=>{switch(method){case 0:source.TakeOrdinary(p,die);break;case 1:source.TakeExtra(p,die);break;case 2:source.TakeTransformed(p,new[]{die},new[]{ManaColor.Blue});break;case 3:source.ClaimForRound(p,ManaColor.Red);break;case 4:source.Take(p,ManaColor.Red);break;default:source.TakeAny(p);break;}});
   Assert.That(source.Dice.Count,Is.EqualTo(3));Assert.That(die.Face,Is.EqualTo(ManaColor.Red));Assert.That(source.WasUsedThisTurn(p),Is.False);Assert.That(p.HeldManaDie,Is.Null);
  }
  [TestCase("basic_card_009")] [TestCase("basic_card_013")]
  public void TransformedDiceRefusedBeforeOriginalManaPayment(string id)
  {
   var(p,c,s,card)=Setup();Use(p,c,s,card,true);var actual=new DeedCard(id,CardType.Action);p.Deck.Hand.Add(actual);p.Mana.AddToken(ManaColor.White);c.ManaDiceToTransform.Add(c.ManaSource.Dice[0]);if(id.EndsWith("013"))c.ManaDiceToTransform.Add(c.ManaSource.Dice[1]);
   Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,actual,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==id),true));Assert.That(p.Mana.Tokens[ManaColor.White],Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(),Is.SameAs(actual));Assert.That(c.ManaSource.Dice.Count,Is.EqualTo(3));
  }
  [Test] public void ClaimRefusesBeforeSpellFeeAndLeavesActualSupplyDie()
  {
   var(p,c,skill,card)=Setup();Use(p,c,skill,card,true);var actual=new DeedCard("magic_012",CardType.Spell);p.Deck.Hand.Add(actual);p.Mana.AddToken(ManaColor.Blue);var spell=CardJsonLoader.LoadSpells().Single(x=>x.Id==actual.Id);
   var data=new MK.Logic.Data.Cards.ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,spell.EnImagePath,spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost);
   Assert.Throws<InvalidOperationException>(()=>CardPlayActions.Play(p,c,actual,data,actionSystem:new ActionSystem(false,new[]{spell})));Assert.That(p.Mana.Tokens[ManaColor.Blue],Is.EqualTo(1));Assert.That(p.Deck.Hand.Single(),Is.SameAs(actual));Assert.That(c.ManaSource.RoundClaims,Is.Empty);
  }
  [Test] public void ReservedTacticManaIsNotSourceUseAndRemainsLegalAfterThree()
  {
   var p=new PlayerState();var game=new GameEngine(new[]{p},3,manaRandom:new Random(173));p.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));game.PrepareNewRound();game.StartNewRound(new[]{OriginalTactics.Get("day_3").Card});
   var die=game.Globals.Mana.Dice[0];die.TurnTo(ManaColor.Green);game.ResolveTacticSelection(p,die:die);var c=new ActionContext();game.BeginNextTurn(c);var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[7];p.Skills.Add(skill);Use(p,c,skill,p.Deck.Hand[0],true);game.UseReservedTacticMana();
   Assert.That(game.Globals.Mana.WasUsedThisTurn(p),Is.False);Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));Assert.That(p.ForgoSourceDiceThisTurn,Is.True);
   CardPlayActions.Play(p,c,p.Deck.Hand[0],CardJsonLoader.LoadBasicActions().Single(x=>x.Id=="basic_card_000"),true);Assert.That(c.MovementPool,Is.EqualTo(4));
  }
  [Test] public void OrdinaryTwoKeepsLaterSourceFreeAndCrystalPaymentIsAllowed()
  {
   var(p,c,s,card)=Setup();Use(p,c,s,card,false);CardManaActions.UseSourceDie(p,c,c.ManaSource.Dice[0],ManaColor.Red);Assert.That(p.ForgoSourceDiceThisTurn,Is.False);Assert.That(c.MeleePool,Is.EqualTo(2));
  }
  [Test] public void CommitmentSurvivesNewContextThenFormalTurnClearsIt()
  {
   var(p,c,s,card)=Setup();Use(p,c,s,card,true);var globals=new GlobalResources(1,()=>DayPart.Day);var later=new ActionContext {ManaSource=globals.Mana};var die=globals.Mana.Dice[0];die.TurnTo(ManaColor.Red);
   Assert.Throws<InvalidOperationException>(()=>CardManaActions.UseSourceDie(p,later,die,ManaColor.Red));p.Mana.AddCrystal(ManaColor.Red);Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Red},later,p),Is.True);
   new PlayerTurnEngine().EndTurn(p,DayPart.Day,c.ManaSource,c);new PlayerTurnEngine().StartTurn(p,DayPart.Day,globals);CardManaActions.UseSourceDie(p,later,die,ManaColor.Red);Assert.That(p.ForgoSourceDiceThisTurn,Is.False);Assert.That(SkillActions.IsUsed(p,s),Is.False);
  }
  [Test] public void FailedInvalidCardDoesNotCommitAndLegacyEffectIsRejected()
  {
   var(p,c,s,card)=Setup();var copy=new DeedCard(card.Id,CardType.Action);Assert.Throws<InvalidOperationException>(()=>Use(p,c,s,copy,true));Assert.That(p.ForgoSourceDiceThisTurn,Is.False);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));Assert.That(c.SidewaysCardValue,Is.Zero);
  }
 }
}
