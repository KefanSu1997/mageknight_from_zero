using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
namespace MK.Tests.Battles
{
 public sealed class PveHiddenFlightSkillTests
 {
  [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
  static (PlayerState p,ActionContext c,SkillCard s) Setup(bool flight)
  {
   var p=new PlayerState();var c=new ActionContext {DayPart=DayPart.Day,MovementMap=new MapState()};
   foreach(var hex in new[]{new AxialCoord(0,0),new AxialCoord(1,0),new AxialCoord(2,0),new AxialCoord(3,0),new AxialCoord(0,1)})c.MovementMap.Placed[hex]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Plains});
   var s=CardJsonLoader.LoadHeroSkills(flight?"hero_skill_pool_005":"hero_skill_pool_001")[flight?6:7];p.Skills.Add(s);return(p,c,s);
  }
  [TestCase(false)] [TestCase(true)] public void HiddenPathPaysBlueOnlyWhenSelectedAndRealMovementConsumesPool(bool blue)
  {
   var(p,c,s)=Setup(false);p.Mana.AddCrystal(ManaColor.Blue);SkillActions.Use(p,c,s,blue?1:0);
   Assert.That(p.Mana.Crystals[ManaColor.Blue],Is.EqualTo(blue?0:1));Assert.That(c.MovementPool,Is.EqualTo(1));Assert.That(c.MountainSafe,Is.True);Assert.That(c.LakeSafe,Is.EqualTo(blue));
   var target=new AxialCoord(1,0);c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,1,new[]{blue?TerrainType.Lake:TerrainType.Mountain});
   var card=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(card);if(!blue)p.Mana.AddToken(ManaColor.Green);
   CardPlayActions.Play(p,c,card,CardJsonLoader.LoadBasicActions().Single(x=>x.Id==card.Id),!blue);
   Assert.That(new MovementService().TryMoveUsingPool(p,target,c.MovementMap,c),Is.True);Assert.That(p.Position,Is.EqualTo(target));Assert.That(c.MovementPool,Is.EqualTo(blue?1:0));Assert.That(CardTravelActions.IsSafeDestination(p,c,target),Is.True);
   new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);Assert.That(c.MountainSafe||c.LakeSafe,Is.False);
  }
  [Test] public void HiddenMissingFeeRefusesBeforeMovementPermissionsAndSkillUse()
  {
   var(p,c,s)=Setup(false);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s,1));Assert.That(c.MovementPool,Is.Zero);Assert.That(c.TerrainCostOverride,Is.Empty);Assert.That(SkillActions.IsUsed(p,s),Is.False);
  }
  [TestCase(1,0)] [TestCase(2,2)] public void FlightActualDistancePaysOnlyPrintedCost(int distance,int cost)
  {
   var(p,c,s)=Setup(true);c.MovementPool=2;var target=new AxialCoord(distance,0);c.MovementMap.Placed[new AxialCoord(1,0)]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Lake});if(distance==1)c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,2,new[]{TerrainType.Forest});
   FlightSkillActions.Move(p,c,s,target);Assert.That(p.Position,Is.EqualTo(target));Assert.That(c.MovementPool,Is.EqualTo(2-cost));Assert.That(c.MovedThisTurn,Is.True);Assert.That(SkillActions.IsUsed(p,s),Is.True);Assert.That(c.TemporalPortalPending||c.IgnoreRampaging,Is.False);
   new PlayerTurnEngine().EndTurn(p,DayPart.Day,new ManaSource(1,()=>DayPart.Day),c);new PlayerTurnEngine().StartTurn(p,DayPart.Day);Assert.That(SkillActions.IsUsed(p,s),Is.True);
  }
  [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] public void InvalidFlightKeepsPositionPoolAndRoundUse(int mode)
  {
   var(p,c,s)=Setup(true);c.MovementPool=1;var target=new AxialCoord(1,0);
   if(mode==0)target=new AxialCoord(2,0);if(mode==1)c.MovementMap.Placed[target]=new MapTile(TileSet.Countryside,0,new[]{TerrainType.Mountain});if(mode==2)c.MovementMap.Places[target]=new PlaceState(PlaceType.Keep){Enemy=CardJsonLoader.LoadMonsters().First()};if(mode==3)c.OtherPlayers.Add(new PlayerState(2,"other"){Position=target});if(mode==4)c.TurnActionCommitted=true;
   Assert.Throws<InvalidOperationException>(()=>FlightSkillActions.Move(p,c,s,target));Assert.That(p.Position,Is.EqualTo(new AxialCoord(0,0)));Assert.That(c.MovementPool,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,s),Is.False);Assert.That(c.MovedThisTurn,Is.False);
  }
  [Test] public void FlightDoesNotProvokeButNextOrdinaryMoveDoes()
  {
   var(p,c,s)=Setup(true);var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_032");var rampage=new AxialCoord(0,1);c.MovementMap.Places[rampage]=new PlaceState(PlaceType.OrcRampaging){Enemy=enemy};
   FlightSkillActions.Move(p,c,s,new AxialCoord(1,0));Assert.That(c.RampagingBattle,Is.Null);Assert.That(c.IgnoreRampaging,Is.False);c.MovementPool=2;
   Assert.That(new MovementService().TryMoveUsingPool(p,new AxialCoord(0,0),c.MovementMap,c),Is.True);Assert.That(c.RampagingBattle,Is.Not.Null);Assert.That(c.Enemies.Single(),Is.SameAs(enemy));
  }
  [Test] public void LegacyFlightCannotCreatePersistentTeleportPermission()
  {
   var(p,c,s)=Setup(true);Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,s));Assert.That(c.TeleportRange,Is.Zero);Assert.That(c.IgnoreRampaging,Is.False);Assert.That(SkillActions.IsUsed(p,s),Is.False);
  }
 }
}
