using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveNorowasPrimaryConsumersTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));

        [Test] public void RecruitedFarmerMarchPaysForestThenRealBattleWoundIsHealedAndSameUnitMovesNextTurn()
        {
            var p = new PlayerState { HeroId="Norowas", Fame=15, Armor=3 };
            var skills = CardJsonLoader.LoadHeroSkills("hero_skill_pool_002");
            HeroSkillAcquisition.Acquire(p,skills[0]);
            HeroSkillAcquisition.Acquire(p,skills[2]);
            Assert.That(p.Level,Is.EqualTo(4));
            Assert.That(p.Skills.Count,Is.EqualTo(p.Level/2));
            foreach(var id in new[]{"basic_card_000","basic_card_008","basic_card_014","basic_card_016","basic_card_021","items_010"})
                p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:CardType.Action));
            var origin = new AxialCoord(0,0); var village = new AxialCoord(0,1); var forest = new AxialCoord(1,0);
            var map = new MapState();
            foreach(var hex in new[]{origin,village,forest})
                map.Placed.Add(hex,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(hex.Equals(forest)?TerrainType.Forest:TerrainType.Plains,6).ToArray()));
            map.Places.Add(village,new PlaceState(PlaceType.Village));
            var actualOffer = CardJsonLoader.LoadUnit("army_2_006");
            var j = new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
                CardJsonLoader.LoadItems(),map:map,unitDeck:new[]{actualOffer});
            j.SelectTactic("day_1");j.BeginTurn();
            DeedCard Card(string id) => p.Deck.Hand.Single(c=>c.Id==id);
            j.PlayOutsideBattle(Card("basic_card_000"),"base");j.Move(village);
            j.BeginVillageInteraction();j.PlayOutsideArtifact(Card("items_010"),false);
            j.RecruitVillageUnit(actualOffer);
            var farmer = p.Units.Single();
            Assert.That(farmer.Card,Is.SameAs(actualOffer));
            Assert.That(j.Game.Globals.Units.Offer,Is.Empty);
            Assert.That(j.Context.InfluencePool,Is.Zero);
            Assert.That(farmer.IsReady,Is.True);Assert.That(farmer.Wounds,Is.Zero);
            j.EndTurn();j.BeginTurn();

            j.UseOutsideSkill(skills[0]);Assert.That(j.Context.MovementPool,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(()=>j.UseOutsideSkill(skills[0]));
            j.PlayOutsideBattle(Card("basic_card_014"),"base");
            Assert.That(j.Context.MovementPool,Is.EqualTo(3));
            j.Move(forest);Assert.That(j.Context.MovementPool,Is.Zero);
            var enemy = CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b = j.BeginBattle(new[]{enemy});b.Advance();b.Advance();
            b.PlanDamage(0,0,farmer);b.Advance();
            Assert.That(farmer.Wounds,Is.EqualTo(1));Assert.That(farmer.IsReady,Is.True);
            Assert.That(farmer.CanActivate,Is.False);Assert.That(p.Wounds,Is.EqualTo(1));
            b.Play(Card("basic_card_021"),"attack");b.Play(Card("basic_card_016"),"attack");
            b.Attack(new[]{0});Assert.That(b.Context.DefeatedEnemyIndices.Contains(0),Is.True);b.Advance();
            j.UseOutsideSkill(skills[2],1,farmer);
            Assert.That(farmer.Wounds,Is.Zero);Assert.That(farmer.IsReady,Is.True);
            Assert.That(farmer.CanActivate,Is.True);Assert.That(p.Wounds,Is.EqualTo(1));
            j.EndTurn();j.BeginTurn();

            Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Units.Single(),Is.SameAs(farmer));
            j.UseOutsideSkill(skills[0]);Assert.That(j.Context.MovementPool,Is.EqualTo(1));
            j.UseOutsideUnit(farmer,2);Assert.That(j.Context.MovementPool,Is.EqualTo(3));
            Assert.That(farmer.IsReady,Is.False);Assert.That(farmer.Wounds,Is.Zero);
            j.SidewaysMovement(Card("basic_card_008"));
            Assert.That(j.Context.MovementPool,Is.EqualTo(4));
            j.Move(village);Assert.That(j.Context.MovementPool,Is.EqualTo(2));
            j.EndTurn();
            Assert.That(j.CompletedTurns,Is.EqualTo(3));Assert.That(j.CompletedBattles,Is.EqualTo(1));
            Assert.That(p.Position,Is.EqualTo(village));Assert.That(p.Wounds,Is.EqualTo(1));
            Assert.That(p.Fame,Is.EqualTo(19));Assert.That(p.Units.Single(),Is.SameAs(farmer));
        }
    }
}
