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
 public sealed class PveChaosSkillTests
 {
  sealed class FaceRandom:Random {readonly int face;public int Calls;public FaceRandom(ManaColor face){this.face=(int)face;}public override int Next(int maxValue){Calls++;return face%maxValue;}}
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static (PlayerState p,ActionContext c,SkillCard s) Setup(ManaColor face)
  {var p=new PlayerState();var c=new ActionContext{DayPart=DayPart.Day};var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[7];HeroSkillAcquisition.Acquire(p,s,new FaceRandom(face));new PlayerTurnEngine().StartTurn(p,DayPart.Day);return(p,c,s);}
  [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)] [TestCase(ManaColor.White)] [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)]
  public void AcquisitionRollsActualSixFaceDieOnceAndDoesNotUseSource(ManaColor face)
  {var p=new PlayerState();var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[7];var rng=new FaceRandom(face);HeroSkillAcquisition.Acquire(p,s,rng);HeroSkillAcquisition.Acquire(p,s,rng);Assert.That(p.ChaosColor,Is.EqualTo(face));Assert.That(rng.Calls,Is.EqualTo(1));Assert.That(p.Skills.Single(),Is.SameAs(s));Assert.That(p.HeldManaDie,Is.Null);Assert.That(p.Mana.GetTotal(),Is.Zero);Assert.Throws<InvalidOperationException>(()=>HeroSkillAcquisition.Acquire(p,s with {},rng));Assert.That(rng.Calls,Is.EqualTo(1));}
  [Test] public void FirstUseRotatesBeforeMovementAndOncePerTurnPersistsColor()
  {var(p,c,s)=Setup(ManaColor.Blue);SkillActions.Use(p,c,s);Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.Green));Assert.That(c.MovementPool,Is.EqualTo(1));Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.Green));Assert.That(p.ChaosUnusedRotationPending,Is.False);}
  [Test] public void BlackIsActualColdFireRangedContribution()
  {var(p,c,s)=Setup(ManaColor.Green);c.InBattle=true;c.CombatPhase=Phase.Ranged;SkillActions.Use(p,c,s);var a=c.CombatPower.Attacks.Single();Assert.That(a.Value,Is.EqualTo(1));Assert.That(a.Element,Is.EqualTo(Element.ColdFire));Assert.That(a.Type,Is.EqualTo(AttackType.Ranged));Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.Black));}
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void GoldExplicitFiveRewardsKeepGoldMarker(int option)
  {var(p,c,s)=Setup(ManaColor.Red);if(option==0){c.InBattle=true;c.CombatPhase=Phase.Block;}if(option==2){c.InBattle=true;c.CombatPhase=Phase.Ranged;}if(option==4){c.InBattle=true;c.CombatPhase=Phase.Melee;}Assert.Throws<InvalidOperationException>(()=>ChaosSkillRules.Validate(p,c));Assert.That(ChaosSkillRules.Options(p,c).Any(x=>x.Option==option),Is.True);SkillActions.Use(p,c,s,option);Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.Gold));Assert.That(option switch{0=>c.BlockPool,1=>c.MovementPool,2=>c.CombatPower.Attacks.Single().Value,3=>c.InfluencePool,_=>c.CombatPower.Attacks.Single().Value},Is.EqualTo(new[]{3,1,1,2,2}[option]));}
  [Test] public void InvalidPhaseLeavesColorResourcesAndUseUntouched()
  {var(p,c,s)=Setup(ManaColor.Gold);Assert.That(ChaosSkillRules.Options(p,c),Is.Empty);var error=Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));Assert.That(error.Message,Does.Contain("当前阶段"));Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.Gold));Assert.That(c.BlockPool,Is.Zero);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [TestCase(false)] [TestCase(true)] public void UnusedTurnMustExplicitlyRotateOrDeclineBeforeNextTurn(bool rotate)
  {var(p,c,s)=Setup(ManaColor.Blue);var engine=new PlayerTurnEngine();engine.EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);Assert.That(p.ChaosUnusedRotationPending,Is.True);Assert.Throws<InvalidOperationException>(()=>engine.StartTurn(p,DayPart.Day));Assert.That(p.IsPersonalTurnActive,Is.False);ChaosSkillRules.ResolveUnusedRotation(p,rotate);Assert.That(p.ChaosColor,Is.EqualTo(rotate?ManaColor.Green:ManaColor.Blue));Assert.That(c.MovementPool+c.InfluencePool+c.BlockPool,Is.Zero);Assert.That(c.CombatPower.Attacks,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.Throws<InvalidOperationException>(()=>ChaosSkillRules.ResolveUnusedRotation(p,true));engine.StartTurn(p,DayPart.Day);Assert.That(p.IsPersonalTurnActive,Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.False);}
  [Test] public void ActiveTurnCannotTakeFreeRotationAndNonactiveEndCannotCreateWindow()
  {var(p,c,s)=Setup(ManaColor.Blue);Assert.Throws<InvalidOperationException>(()=>ChaosSkillRules.ResolveUnusedRotation(p,true));var other=new PlayerState();HeroSkillAcquisition.Acquire(other,s,new FaceRandom(ManaColor.Blue));ChaosSkillRules.RecordEndTurn(other);Assert.That(other.ChaosUnusedRotationPending,Is.False);}
  [Test] public void HeadbandBurnAcquiresActualChaosAndNormalBorrowRejectsBeforeArtifactMoves()
  {var p=new PlayerState();var c=new ActionContext{SkillSupply=new SkillSupply()};var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[7];c.SkillSupply.Offer.Add(s);var item=new DeedCard("items_022",CardType.Artifact);p.Deck.Hand.Add(item);var data=CardJsonLoader.LoadItems().Single(x=>x.Id==item.Id);Assert.Throws<InvalidOperationException>(()=>CardPlayActions.PlayArtifact(p,c,item,data));Assert.That(p.Deck.Hand.Single(),Is.SameAs(item));Assert.That(p.ChaosColor,Is.Null);CardPlayActions.PlayArtifact(p,c,item,data,true);Assert.That(p.Skills.Single(),Is.SameAs(s));Assert.That(p.ChaosColor,Is.Not.Null);Assert.That(c.SkillSupply.Offer,Is.Empty);Assert.That(p.Deck.RemovedCards.Single(),Is.SameAs(item));}
  [Test] public void ActualLevelRewardAcquiresCommonChaosAndRollsAtAcquisition()
  {var p=new PlayerState{HeroId="Tovak"};var actions=new AdvActionSupply();actions.SetDeck(CardJsonLoader.LoadAdvancedActions().Select(x=>new AdvActionCard(x.Id)));actions.FillVacancies();var common=new SkillSupply();var chaos=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[7];common.Offer.Add(chaos);var progression=new HeroProgression(p,CardJsonLoader.LoadHeroSkills("hero_skill_pool_007"),actions,common,()=>true,new FaceRandom(ManaColor.White));p.Fame=3;progression.PrepareNext();var action=actions.Offer[0];progression.Choose(chaos,action);Assert.That(p.Level,Is.EqualTo(2));Assert.That(p.Skills.Single(),Is.SameAs(chaos));Assert.That(p.ChaosColor,Is.EqualTo(ManaColor.White));Assert.That(common.Offer.Any(x=>ReferenceEquals(x,chaos)),Is.False);Assert.That(p.Deck.DrawPileCount,Is.EqualTo(1));}
 }
}
