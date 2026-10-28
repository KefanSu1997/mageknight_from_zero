using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;
namespace MK.Tests.Battles
{
    public sealed class PveWildEagleSkillTests
    {
        private sealed class Quiet : Random { public override int Next(int maximum)=>maximum-1; }
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private SkillCard Own(PlayerState p,bool eagle){var s=CardJsonLoader.LoadHeroSkills(eagle?"hero_skill_pool_004":"hero_skill_pool_001")[eagle?2:1];p.Skills.Add(s);return s;}
        private MapTile Tile(int id)=>new MapTile(TileSet.Countryside,id,Enumerable.Repeat(TerrainType.Plains,6).ToArray());
        [TestCase(false,DayPart.Day,1)] [TestCase(false,DayPart.Night,1)]
        [TestCase(true,DayPart.Day,2)] [TestCase(true,DayPart.Night,1)]
        public void OwnedPassiveDiscountWorksBeforeActivationAndAcrossFormalTurn(bool eagle,DayPart day,int expected)
        {
            var p=new PlayerState();var skill=Own(p,eagle);var c=new ActionContext{DayPart=day,SkipDraw=true};
            Assert.That(ExplorationService.Cost(p,c),Is.EqualTo(expected));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            new PlayerTurnEngine().EndTurn(p,day,null,c);Assert.That(ExplorationService.Cost(p,c),Is.EqualTo(expected));
            p.Skills.Remove(skill);Assert.That(ExplorationService.Cost(p,c),Is.EqualTo(2));
        }
        [Test] public void ExistingExplorationServicePaysSamePreviewCostAndPreservesDrawnTileIdentity()
        {
            // Existing MapTile remains the repository's abstract tile model, not proof of original seven-hex topology.
            var p=new PlayerState();Own(p,false);var map=new MapState();map.Placed.Add(p.Position,Tile(0));var drawn=Tile(1);map.Countryside.SetCards(new[]{drawn});
            var c=new ActionContext{MovementMap=map,MovementPool=0};var service=new ExplorationService(map,new Quiet());
            Assert.That(service.ExploreUsingPool(p,new AxialCoord(1,0),0,c).Success,Is.False);Assert.That(map.Countryside.Count,Is.EqualTo(1));
            c.MovementPool=1;var result=service.ExploreUsingPool(p,new AxialCoord(1,0),60,c);
            Assert.That(result.Success,Is.True);Assert.That(c.MovementPool,Is.Zero);Assert.That(map.Placed[new AxialCoord(1,0)],Is.SameAs(drawn));Assert.That(map.Countryside.Count,Is.Zero);
        }
        [TestCase(PlaceType.Keep)] [TestCase(PlaceType.MageTower)] [TestCase(PlaceType.City)]
        public void EagleDaytimeRevealsSameActualFortifiedGuardsAtTwoBeforeActivation(PlaceType type)
        {
            var p=new PlayerState();var skill=Own(p,true);var map=new MapState();var at=new AxialCoord(2,0);map.Placed.Add(at,Tile(2));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var site=new PlaceState(type){Enemy=enemy};map.Places.Add(at,site);
            SiteVisibility.RevealVisible(p,map,DayPart.Night);Assert.That(map.RevealedSites,Is.Empty);
            SiteVisibility.RevealVisible(p,map,DayPart.Day);Assert.That(map.RevealedSites.Contains(site),Is.True);Assert.That(site.Defenders.Single(),Is.SameAs(enemy));
            Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            var s=new PveBattleSession(p,site.Defenders,CardJsonLoader.LoadBasicActions());Assert.That(s.Enemies.Single(),Is.SameAs(enemy));
        }
        [Test] public void FormalJourneyTurnStartRevealsOwnedEagleTwoAwayWithoutActiveSkillUse()
        {
            var p=new PlayerState{HeroId="Wolfhawk",Fame=3};var skill=Own(p,true);
            p.Deck.SetDeck(Enumerable.Range(0,8).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
            var map=new MapState();foreach(int q in new[]{0,1,2})map.Placed.Add(new AxialCoord(q,0),Tile(q));
            var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var site=new PlaceState(PlaceType.Keep){Enemy=enemy};map.Places.Add(new AxialCoord(2,0),site);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map);j.SelectTactic("day_1");
            Assert.That(map.RevealedSites,Is.Empty);j.BeginTurn();
            Assert.That(j.Context.DayPart,Is.EqualTo(DayPart.Day));Assert.That(map.RevealedSites.Contains(site),Is.True);
            Assert.That(site.Defenders.Single(),Is.SameAs(enemy));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            Assert.That(j.Context.MovementPool,Is.Zero);Assert.That(p.Position,Is.EqualTo(new AxialCoord(0,0)));
        }
        [Test] public void EagleRangeDoesNotRevealThreeAwayOrNightUnderDaylightAmulet()
        {
            var p=new PlayerState();Own(p,true);var map=new MapState();
            foreach(int distance in new[]{2,3}){var at=new AxialCoord(distance,0);map.Placed.Add(at,Tile(distance));map.Places.Add(at,new PlaceState(PlaceType.Keep));}
            SiteVisibility.RevealVisible(p,map,DayPart.Night,new ActionContext{RevealSitesAsDay=true});Assert.That(map.RevealedSites,Is.Empty);
            SiteVisibility.RevealVisible(p,map,DayPart.Day);Assert.That(map.RevealedSites.Count,Is.EqualTo(1));Assert.That(map.RevealedSites.Contains(map.Places[new AxialCoord(3,0)]),Is.False);
        }
        [Test] public void EagleActualMovementActivationOnceDoesNotStackPassiveDiscount()
        {
            var p=new PlayerState();var skill=Own(p,true);var c=new ActionContext{DayPart=DayPart.Night};
            SkillActions.Use(p,c,skill);Assert.That(c.MovementPool,Is.EqualTo(1));Assert.That(ExplorationService.Cost(p,c),Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill));Assert.That(c.MovementPool,Is.EqualTo(1));
        }
        [Test] public void WildReductionTargetsOneActualAttackSlotAndItsDamageConsumer()
        {
            var p=new PlayerState{Armor=2};var skill=Own(p,false);var original=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var enemy=original with{Attacks=new[]{new EnemyAttack(3,Element.Physical),new EnemyAttack(3,Element.Physical)}};
            var c=new ActionContext{InBattle=true,CombatPhase=Phase.AssignDamage,Enemies=new[]{enemy},SelectedEnemy=enemy,SelectedEnemyAttackIndex=1};
            SkillActions.Use(p,c,skill,1);Assert.That(CardCombatActions.DefenseProfile(enemy,0,c,0).Attack,Is.EqualTo(original.Attack));Assert.That(CardCombatActions.DefenseProfile(enemy,0,c,1).Attack,Is.EqualTo(2));
            var logger=new ListGameLogger();var result=BattleResolver.Resolve(p,c.Enemies,Array.Empty<BlockAllocation>(),Array.Empty<AttackAllocation>(),ctx:c,logger:logger);
            Assert.That(result.TotalWounds,Is.EqualTo(3),string.Join(";",logger.Entries));Assert.That(p.Wounds,Is.EqualTo(3));Assert.That(ExplorationService.Cost(p,c),Is.EqualTo(1));
        }
        [Test] public void WildAttackAddsOneAndFormalAttackConsumesIt()
        {
            var p=new PlayerState();var skill=Own(p,false);var enemy=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var s=new PveBattleSession(p,new[]{enemy},CardJsonLoader.LoadBasicActions());s.Advance();s.Advance();s.Advance();
            SkillActions.Use(p,s.Context,skill);s.Context.CombatPower.AddAttack(2);Assert.That(s.Attack(0).Kills,Is.EqualTo(1));Assert.That(SkillActions.IsUsed(p,skill),Is.True);
        }
        [Test] public void WildRejectedActualTargetPreservesOncePerTurnActivation()
        {
            var p=new PlayerState();var skill=Own(p,false);var c=new ActionContext{InBattle=true,CombatPhase=Phase.Block};
            Assert.That(skill.Frequency,Is.EqualTo(SkillFrequency.OncePerTurn));Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,1));Assert.That(SkillActions.IsUsed(p,skill),Is.False);
        }
    }
}
