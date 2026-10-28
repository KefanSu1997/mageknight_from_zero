using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveDungeonJourneyTests
 {
  sealed class FaceRandom:Random {readonly int face;public FaceRandom(ManaColor face){this.face=(int)face;}public override int Next(int max)=>face%max;}
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static PveJourneySession Start(bool conquered=false,bool bound=false)
  {
   var p=new PlayerState{HeroId="Norowas",Fame=3};p.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));p.Deck.Hand.Add(new DeedCard("magic_009",CardType.Spell));p.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));p.Mana.AddCrystal(ManaColor.Red,2);
   if(bound){var supply=new UnitSupply();supply.SetDeck(new[]{CardJsonLoader.LoadUnit("army_2_006"),CardJsonLoader.LoadUnit("army_2_006")});HeroSkillAcquisition.Acquire(p,CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[7],units:supply);var recruitment=new ActionContext{UseLoyaltySlot=true};Assert.That(CardUnitActions.TryRecruit(p,recruitment,supply.Offer,0,RecruitLocation.Village),Is.True);p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_006")));}
   var map=new MapState();map.Placed[p.Position]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});map.Places[p.Position]=new PlaceState(PlaceType.Dungeon){DungeonConquered=conquered,OwnerId=conquered?99:(int?)null};
   var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_066");Assert.That(enemy.TokenColor,Is.EqualTo(EnemyTokenColor.Brown));var source=new EnemyTokenSupply(new[]{enemy},new Random(1));
   var spells=CardJsonLoader.LoadSpells().ToArray();var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(x=>new ActionCardData(x.Id,x.Name,x.Set,x.ImagePath,x.EnImagePath,x.Top.Effect,x.Bottom.Effect,x.Top.ManaCost)));
   var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,enemySupply:source,manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1));j.SelectTactic("day_1");j.BeginTurn();return j;
  }
  static void Finish(PveJourneySession j){while(j.Battle.Phase!=Phase.End)j.Battle.Advance();}
  static void Win(PveJourneySession j)
  {var die=j.Context.ManaSource.Dice.First();die.TurnTo(ManaColor.Black);CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.Black);var card=j.Player.Deck.Hand.First(x=>x.Id=="magic_009");j.Battle.Play(card,"strong");Assert.That(j.Battle.Attack(0).Kills,Is.EqualTo(1));Finish(j);Assert.That(j.Battle.Won,Is.True);}
  [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)] [TestCase(ManaColor.Red)]
  public void ActualDungeonNightBattleDiscardsBrownAndAwardsSelectedRealSupplyAtTurnEnd(ManaColor reward)
  {
   var j=Start();var original=j.Context;var enemy=j.EnemySupply.DrawPile.Single();int fame=j.Player.Fame;j.EnterDungeon(new FaceRandom(reward));Assert.That(j.Battle.Context,Is.SameAs(original));Assert.That(j.Battle.Enemies.Single(),Is.SameAs(enemy));Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Night));Assert.That(j.Game.Clock.DayPart,Is.EqualTo(DayPart.Day));Assert.That(j.Context.UnitsAllowed,Is.False);Assert.That(j.Context.TurnActionCommitted,Is.True);Win(j);
   var d=j.Context.Dungeon;Assert.That(d.RewardDie,Is.EqualTo(reward));Assert.That(j.EnemySupply.DiscardPile.Single(),Is.SameAs(enemy));Assert.That(j.EnemySupply.InUse,Is.Empty);Assert.That(d.Site.Conquered,Is.True);Assert.That(d.Site.OwnerId,Is.EqualTo(j.Player.Id));Assert.That(j.Player.Fame,Is.EqualTo(fame+enemy.Fame));Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Day));Assert.That(j.Context.UnitsAllowed,Is.True);
   var hand=j.Player.Deck.Hand.ToArray();Assert.Throws<InvalidOperationException>(()=>j.EndTurn());Assert.That(j.Player.Deck.Hand,Is.EqualTo(hand));Assert.That(j.Game.IsEndingTurn,Is.False);string id;CardType type;
   if(reward==ManaColor.Red){var options=j.RevealDungeonArtifactReward();Assert.That(options.Count,Is.EqualTo(2));var a=options[1];id=a.Id;type=CardType.Artifact;Assert.Throws<InvalidOperationException>(()=>j.SelectDungeonArtifactReward(new ArtifactCard(a.Id)));j.SelectDungeonArtifactReward(a);Assert.That(j.RevealDungeonArtifactReward()[1],Is.SameAs(a));}
   else{var a=j.Game.Globals.Spells.Offer[0];id=a.Id;type=CardType.Spell;j.SelectSiteSpellReward(a);}
   Assert.That(j.Player.Deck.Hand.Any(x=>x.Id==id),Is.False);j.EndTurn();Assert.That(d.RewardGranted,Is.True);Assert.That(j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).Count(x=>x.Id==id&&x.Type==type),Is.EqualTo(1));Assert.That(j.CompletedBattles,Is.EqualTo(1));
  }
  [Test] public void DefeatStillDiscardsAndSamePhysicalBrownCanReturnOnNextEncounter()
  {var j=Start();j.EnterDungeon();var first=j.Battle.Enemies.Single();Finish(j);Assert.That(j.Context.Dungeon.Won,Is.False);Assert.That(j.Context.Dungeon.Site.Conquered,Is.False);Assert.That(j.Context.Dungeon.Reward,Is.EqualTo(DungeonRewardKind.None));Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Day));var discard=j.Player.Deck.Hand.First(x=>x.Type!=CardType.Wound);j.EndTurn(new[]{discard});j.BeginTurn();j.EnterDungeon();Assert.That(j.Battle.Enemies.Single(),Is.SameAs(first));Assert.That(j.EnemySupply.DiscardPile,Is.Empty);Assert.That(j.EnemySupply.InUse.Single(),Is.SameAs(first));}
  [Test] public void ConqueredRevisitGivesOnlyFameAndPreservesPriorShield()
  {var j=Start(true);j.EnterDungeon(new FaceRandom(ManaColor.Gold));Win(j);Assert.That(j.Context.Dungeon.RewardDie,Is.Null);Assert.That(j.Context.Dungeon.Reward,Is.EqualTo(DungeonRewardKind.None));Assert.That(j.Context.Dungeon.Site.OwnerId,Is.EqualTo(99));j.EndTurn();j.BeginTurn();j.EnterDungeon();Assert.That(j.Battle.Enemies.Single().Id,Is.EqualTo("monster_066"));}
  [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidEntryPreservesOriginalSupplyAndDay(int reason)
  {var j=Start();if(reason==0)j.Game.Map.Places.Clear();if(reason==1)j.Context.TurnActionCommitted=true;if(reason==2)j.Context.RestedThisTurn=true;var before=j.EnemySupply.DrawPile.Single();Assert.Throws<InvalidOperationException>(()=>j.EnterDungeon());Assert.That(j.EnemySupply.DrawPile.Single(),Is.SameAs(before));Assert.That(j.EnemySupply.InUse,Is.Empty);Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Day));Assert.That(j.Context.Dungeon,Is.Null);Assert.That(j.Context.InBattle,Is.False);}
  [Test] public void ActualLoyaltyRecruitParticipatesButOrdinaryUnitCannotActOrAbsorb()
  {var j=Start(bound:true);var loyal=j.Player.Units.Single(u=>LoyaltyActions.IsBound(j.Player,u));var ordinary=j.Player.Units.Single(u=>!ReferenceEquals(u,loyal));j.EnterDungeon();j.Battle.Advance();Assert.Throws<InvalidOperationException>(()=>j.Battle.UseUnit(ordinary,0,1));Assert.That(ordinary.IsReady,Is.True);j.Battle.UseUnit(loyal,0,1);Assert.That(loyal.IsReady,Is.False);j.Battle.Block(0,0);j.Battle.Advance();Assert.Throws<InvalidOperationException>(()=>j.Battle.PlanDamage(0,1,ordinary));Assert.That(ordinary.Wounds,Is.Zero);j.Battle.PlanDamage(0,1,loyal);j.Battle.Advance();Assert.That(loyal.Wounds,Is.EqualTo(1));Assert.That(ordinary.Wounds,Is.Zero);j.Battle.Advance();Assert.That(j.Context.UnitsAllowed,Is.True);Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Day));}
  [Test] public void ArtifactRewardDoesNotHalfDrawAndReturnsUnselectedActualCardToSupply()
  {var j=Start();var a=new ArtifactCard("items_000");var b=new ArtifactCard("items_001");j.Game.Globals.Artifacts.SetDeck(new[]{a});j.EnterDungeon(new FaceRandom(ManaColor.Blue));Win(j);Assert.Throws<InvalidOperationException>(()=>j.RevealDungeonArtifactReward());Assert.That(j.Game.Globals.Artifacts.Count,Is.EqualTo(1));Assert.That(j.Context.Dungeon.ArtifactChoices,Is.Empty);Assert.Throws<InvalidOperationException>(()=>j.EndTurn());j.Game.Globals.Artifacts.SetDeck(new[]{a,b});var choices=j.RevealDungeonArtifactReward();Assert.That(choices[0],Is.SameAs(a));Assert.That(choices[1],Is.SameAs(b));j.SelectDungeonArtifactReward(b);j.EndTurn();Assert.That(j.Game.Globals.Artifacts.Count,Is.EqualTo(1));Assert.That(j.Game.Globals.Artifacts.Draw(),Is.SameAs(a));Assert.That(j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).Count(x=>x.Id==b.Id),Is.EqualTo(1));}
 }
}
