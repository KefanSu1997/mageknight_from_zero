using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveManaSkillPaymentTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static (PlayerState p,ActionContext c,SkillCard s) Setup(bool polarization=false)
  {var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Day};var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[polarization?7:6];p.Skills.Add(s);return(p,c,s);}
  static DeedCard Hand(PlayerState p,string id="basic_card_000",CardType type=CardType.Action){var a=new DeedCard(id,type);p.Deck.Hand.Add(a);return a;}
  static void Play(PlayerState p,ActionContext c,DeedCard card,bool strong=true)=>CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),strong);
  static void Spell(PlayerState p,ActionContext c,DeedCard card,bool strong)
  {var s=CardJsonLoader.LoadSpells().Single(x=>x.Id==card.Id);var d=new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost);CardPlayActions.Play(p,c,card,d,strong,actionSystem:new ActionSystem(false,new[]{s}));}
  [Test] public void InvocationDiscardsExactNonwoundAndImmediatelyPowersOriginalMove()
  {var(p,c,s)=Setup();var card=Hand(p);var material=Hand(p);var spare=Hand(p);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,material),()=>Play(p,c,card));Assert.That(c.MovementPool,Is.EqualTo(4));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(material));Assert.That(p.Deck.Hand.Single(),Is.SameAs(spare));Assert.That(p.Deck.PlayedCards.Single(),Is.SameAs(card));Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.True);}
  [Test] public void InvocationWoundPowersActualFireSpellWithoutLeavingMana()
  {var(p,c,s)=Setup();c.InBattle=true;var card=Hand(p,"magic_009",CardType.Spell);var wound=Hand(p,"wound",CardType.Wound);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Red,wound),()=>Spell(p,c,card,false));Assert.That(c.CombatPower.Attacks.Single().Value,Is.EqualTo(5));Assert.That(p.Deck.DiscardPile.Single(),Is.SameAs(wound));Assert.That(p.Mana.GetTotal(),Is.Zero);}
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] public void InvalidInvocationMaterialCannotMutateAnything(int mode)
  {var(p,c,s)=Setup();var card=Hand(p);var material=mode==0?card:Hand(p);if(mode==1)c.CardToDiscard=material;if(mode==2)p.Deck.Hand.Remove(material);if(mode==3)c.CardToRemove=material;int count=p.Deck.Hand.Count;Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,material),()=>Play(p,c,card)));Assert.That(p.Deck.Hand.Count,Is.EqualTo(count));Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(c.MovementPool,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void MissingOtherSpellFeeKeepsMaterialAndSkill()
  {var(p,c,s)=Setup();c.DayPart=DayPart.Night;c.InBattle=true;var card=Hand(p,"magic_009",CardType.Spell);var wound=Hand(p,"wound",CardType.Wound);Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Black,wound),()=>Spell(p,c,card,true)));Assert.That(p.Deck.Hand,Does.Contain(wound));Assert.That(p.Deck.DiscardPile,Is.Empty);Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void OppositeCrystalRecordsSourceButCurseSeesPaidTarget()
  {var(p,c,s)=Setup(true);var card=Hand(p);p.Mana.AddCrystal(ManaColor.Red);p.ManaCurseColor=ManaColor.Green;ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,SourceColor:ManaColor.Red),()=>Play(p,c,card));Assert.That(c.SpentCrystals.GetValueOrDefault(ManaColor.Red),Is.EqualTo(1));Assert.That(c.SpentCrystals.GetValueOrDefault(ManaColor.Green),Is.Zero);Assert.That(p.Wounds,Is.EqualTo(1));Assert.That(c.MovementPool,Is.EqualTo(4));}
  [Test] public void PreviewUsesRealSourceCrystalAndDoesNotConsumeSkill()
  {var(p,c,s)=Setup(true);p.Mana.AddCrystal(ManaColor.Red);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,SourceColor:ManaColor.Red),()=>{Assert.That(p.Mana.TryPreviewExactColors(new[]{ManaColor.Green},c,out var left),Is.True);Assert.That(left[ManaColor.Red],Is.Zero);});Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [TestCase(false)] [TestCase(true)] public void DayBlackActualSourceDiePaysOriginalFeeAndRecordsHistory(bool extra)
  {var(p,c,s)=Setup(true);c.ManaSource=new ManaSource(1,()=>DayPart.Day);var die=c.ManaSource.Dice.First();die.TurnTo(ManaColor.Black);c.ExtraManaDice=extra?1:0;var card=Hand(p);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,SourceColor:ManaColor.Black,SourceDie:die,ExtraDie:extra),()=>Play(p,c,card));Assert.That(c.ManaSource.TurnClaims[die],Is.SameAs(p));Assert.That(c.ManaSource.WasUsedThisTurn(p),Is.True);Assert.That(p.HeldManaDie,extra?Is.Null:Is.SameAs(die));Assert.That(c.ExtraManaDice,Is.Zero);Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(c.BlackManaWild,Is.False);}
  [Test] public void NightGoldPaysOnlyStrongSpellBlackItem()
  {var(p,c,s)=Setup(true);c.DayPart=DayPart.Night;c.InBattle=true;c.ManaSource=new ManaSource(1,()=>DayPart.Night);var die=c.ManaSource.Dice.First();die.TurnTo(ManaColor.Gold);p.Mana.AddToken(ManaColor.Red);var card=Hand(p,"magic_009",CardType.Spell);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Black,SourceColor:ManaColor.Gold,SourceDie:die),()=>Spell(p,c,card,true));Assert.That(c.CombatPower.Attacks,Is.Not.Empty);Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(c.GoldManaWild,Is.False);Assert.That(c.ManaSource.TurnClaims[die],Is.SameAs(p));}
  [Test] public void NightGoldCannotPayArbitraryBlackCost()
  {var(p,c,s)=Setup(true);c.DayPart=DayPart.Night;p.Mana.AddToken(ManaColor.Gold);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Black,SourceColor:ManaColor.Gold),()=>Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Black},c,p),Is.False));Assert.That(p.Mana.Tokens[ManaColor.Gold],Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [TestCase(false)] [TestCase(true)] public void ActualFireMageUsesSharedPaymentAndExhausts(bool polarization)
  {var(p,c,s)=Setup(polarization);c.InBattle=true;c.CombatPhase=Phase.Melee;var u=new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"),"army_007"));p.Units.Add(u);var wound=polarization?null:Hand(p,"wound",CardType.Wound);if(polarization)p.Mana.AddCrystal(ManaColor.Green);ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Red,wound,polarization?ManaColor.Green:(ManaColor?)null),()=>CardUnitActions.Use(p,c,u,1));Assert.That(u.IsReady,Is.False);Assert.That(c.CombatPower.Attacks.Single().Value,Is.EqualTo(6));Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.True);}
  [TestCase(false)] [TestCase(true)] public void FreeCardAndLegacySkillEntryRejectWithoutDiscard(bool legacy)
  {var(p,c,s)=Setup();var card=Hand(p);var material=Hand(p);Assert.Throws<InvalidOperationException>(()=>{if(legacy)SkillActions.Use(p,c,s);else ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,material),()=>Play(p,c,card,false));});Assert.That(p.Deck.Hand.Count,Is.EqualTo(2));Assert.That(c.MovementPool,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void FailedDiePaymentDoesNotClaimDieOrConsumeOtherMana()
  {var(p,c,s)=Setup(true);c.DayPart=DayPart.Night;c.InBattle=true;c.ManaSource=new ManaSource(1,()=>DayPart.Night);var die=c.ManaSource.Dice.First();die.TurnTo(ManaColor.Gold);var card=Hand(p,"magic_009",CardType.Spell);Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Black,SourceColor:ManaColor.Gold,SourceDie:die),()=>Spell(p,c,card,true)));Assert.That(c.ManaSource.Dice,Does.Contain(die));Assert.That(p.HeldManaDie,Is.Null);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [TestCase(ManaColor.Red,ManaColor.Green)] [TestCase(ManaColor.Green,ManaColor.Red)] [TestCase(ManaColor.Blue,ManaColor.White)] [TestCase(ManaColor.White,ManaColor.Blue)]
  public void EveryPrintedBasicPairConsumesTokenBeforeCrystal(ManaColor from,ManaColor to)
  {var(p,c,s)=Setup(true);p.Mana.AddToken(from);p.Mana.AddCrystal(from);ManaSkillPaymentActions.Execute(p,c,new(s,to,SourceColor:from),()=>Assert.That(p.Mana.TryPayExactColors(new[]{to},c,p),Is.True));Assert.That(p.Mana.Tokens.GetValueOrDefault(from),Is.Zero);Assert.That(p.Mana.Crystals[from],Is.EqualTo(1));Assert.That(c.SpentCrystals,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.True);}
  [Test] public void DuplicateCostsReplaceOnlyOneAndFailureRemainsAtomic()
  {var(p,c,s)=Setup(true);p.Mana.AddCrystal(ManaColor.Red);var selection=new ManaSkillPaymentSelection(s,ManaColor.Green,SourceColor:ManaColor.Red);ManaSkillPaymentActions.Execute(p,c,selection,()=>Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Green,ManaColor.Green},c,p),Is.False));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);p.Mana.AddToken(ManaColor.Green);ManaSkillPaymentActions.Execute(p,c,selection,()=>Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Green,ManaColor.Green},c,p),Is.True));Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.That(p.Mana.TryPayExactColors(new[]{ManaColor.Green},c,p),Is.False);}
  [TestCase(false)] [TestCase(true)] public void SourcePromiseOrNoExtraQuotaRejectBeforeCommand(bool forbidden)
  {var(p,c,s)=Setup(true);c.ManaSource=new ManaSource(1,()=>DayPart.Day);var die=c.ManaSource.Dice.First();die.TurnTo(ManaColor.Black);if(forbidden){var repel=CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[7];p.Skills.Add(repel);var side=Hand(p);SkillActions.PlaySideways(p,c,repel,side,SidewaysType.Influence,forgoSourceDice:true);}var card=Hand(p);Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.Execute(p,c,new(s,ManaColor.Green,SourceColor:ManaColor.Black,SourceDie:die,ExtraDie:!forbidden),()=>Play(p,c,card)));Assert.That(c.ManaSource.Dice,Does.Contain(die));Assert.That(c.MovementPool,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void OncePerTurnCannotRepeatAndFormalNewTurnRestores()
  {var(p,c,s)=Setup(true);p.Mana.AddCrystal(ManaColor.Red,2);var a=Hand(p);var b=Hand(p);var selection=new ManaSkillPaymentSelection(s,ManaColor.Green,SourceColor:ManaColor.Red);ManaSkillPaymentActions.Execute(p,c,selection,()=>Play(p,c,a));Assert.Throws<InvalidOperationException>(()=>ManaSkillPaymentActions.Execute(p,c,selection,()=>Play(p,c,b)));var engine=new PlayerTurnEngine();engine.EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);engine.StartTurn(p,DayPart.Day);var next=Hand(p);ManaSkillPaymentActions.Execute(p,c,selection,()=>Play(p,c,next));Assert.That(p.Mana.Crystals[ManaColor.Red],Is.Zero);}
 }
}


