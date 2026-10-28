using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

namespace MK.Tests.Map
{
    public sealed class OriginalLoyaltySelectionTests
    {
        [Test] public void SelectionRequiresActualBoundSiteAndDoesNotRecruitOrSpend()
        {
            CardJsonLoader.SetBaseDirectory("resources/text_json");
            var enemies=new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Where(e=>e.TokenColor==EnemyTokenColor.Green));
            var map=new MapState();OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map,enemies);
            var originals=CardJsonLoader.LoadBasicActions().ToArray();var player=OriginalHeroes.Get("Norowas").CreatePlayer(originals,new Random(5));
            player.Fame=3;player.Position=map.Places.First(p=>p.Value.Type==PlaceType.Village).Key;
            var journey=new PveJourneySession(player,originals,map:map);
            HeroSkillAcquisition.Acquire(player,CardJsonLoader.LoadHeroSkills("hero_skill_pool_002")[7],units:journey.Game.Globals.Units);
            journey.SelectTactic("day_1");journey.BeginTurn();
            Assert.Throws<InvalidOperationException>(()=>journey.SelectLoyaltyRecruit(true));
            journey.BeginSiteInteraction();var offer=journey.Game.Globals.Units.Offer.ToArray();var slots=player.CommandSlots;
            journey.SelectLoyaltyRecruit(true);
            Assert.That(journey.Context.UseLoyaltySlot,Is.True);Assert.That(player.Units,Is.Empty);
            Assert.That(player.CommandSlots,Is.EqualTo(slots));Assert.That(journey.Game.Globals.Units.Offer,Is.EqualTo(offer));
            Assert.That(journey.Context.InfluencePool,Is.Zero);
            journey.SelectLoyaltyRecruit(false);Assert.That(journey.Context.UseLoyaltySlot,Is.False);
        }
    }
}
