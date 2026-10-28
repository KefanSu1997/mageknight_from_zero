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
    public sealed class PveBraevalarPrimaryConsumersTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));

        [Test] public void ActualTowerTranceCompletesRecruitmentFeeThenElementalResistancePreventsOriginalIceHit()
        {
            var p = new PlayerState { HeroId="Braevalar", Fame=35, Armor=3 };
            var skills = CardJsonLoader.LoadHeroSkills("hero_skill_pool_001");
            HeroSkillAcquisition.Acquire(p,skills[0]);HeroSkillAcquisition.Acquire(p,skills[4]);
            Assert.That(p.Level,Is.EqualTo(6));Assert.That(p.Skills.Count,Is.LessThanOrEqualTo(p.Level/2));
            foreach(var id in new[]{"basic_card_008","advanced_card_000","magic_004","magic_009","items_007","items_010","items_015"})
                p.Deck.Hand.Add(new DeedCard(id,id.StartsWith("items_")?CardType.Artifact:id.StartsWith("magic_")?CardType.Spell:CardType.Action));
            p.Mana.AddCrystal(ManaColor.Red);p.Mana.AddCrystal(ManaColor.Blue);
            var mountain=new AxialCoord(0,-1);var tower=new AxialCoord(-1,-1);
            // Same existing journey origin -> mountain -> Mage Tower geometry;
            // the place starts guarded and is conquered through the real spell/battle.
            var map = new MapState();
            foreach(var hex in new[]{new AxialCoord(0,0),mountain,tower})
                map.Placed.Add(hex,new MapTile(TileSet.Countryside,0,Enumerable.Repeat(hex.Equals(mountain)?TerrainType.Mountain:TerrainType.Plains,6).ToArray()));
            var guard=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_002");
            var supply=new EnemyTokenSupply(new[]{guard},new Random(0));
            var site=new PlaceState(PlaceType.MageTower){Enemy=supply.Draw(EnemyTokenColor.Purple)};
            map.Places.Add(tower,site);Assert.That(site.Conquered,Is.False);
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var deeds=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(s=>new ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost)));
            var offer=CardJsonLoader.LoadUnit("army_2_001");
            var j=new PveJourneySession(p,deeds,CardJsonLoader.LoadItems(),map:map,spells:spells,
                unitDeck:new[]{offer},enemySupply:supply,supplyRandom:new Random(0),manaRandom:new Random(0));
            j.SelectTactic("day_1");j.BeginTurn();
            DeedCard Card(string id)=>p.Deck.Hand.Single(c=>c.Id==id);
            j.PlayOutsideArtifact(Card("items_007"),false);
            j.PlayOutsideArtifact(Card("items_015"),true,null,ManaColor.Black,ManaColor.Black,ManaColor.Red);
            j.PlayOutsideWithInputs(Card("magic_004"),"strong",new PveDeedSelection{TravelPath=new[]{mountain,tower}});
            var first=j.Battle;Assert.That(first.Enemies.Single(),Is.SameAs(guard));
            first.Play(Card("magic_009"),"base");first.Attack(new[]{0});
            for(int i=0;i<4;i++)first.Advance();
            j.FinishTunnelAssault(false);
            Assert.That(site.Conquered,Is.True);Assert.That(site.OwnerId,Is.EqualTo(p.Id));
            Assert.That(supply.DiscardPile.Single(),Is.SameAs(guard));
            j.SelectSiteSpellReward(j.Game.Globals.Spells.Offer[0]);j.EndTurn();j.BeginTurn();

            Assert.That(p.Position,Is.EqualTo(tower));j.BeginSiteInteraction();
            Assert.That(j.Context.InfluencePool,Is.EqualTo(-1));
            j.PlayOutsideArtifact(Card("items_010"),false);j.PlayOutsideBattle(Card("basic_card_008"),"base");
            Assert.That(j.Context.InfluencePool,Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(()=>j.RecruitSiteUnit(offer));
            Assert.That(j.Game.Globals.Units.Offer.Single(),Is.SameAs(offer));
            j.UseOutsideSkill(skills[4]);Assert.That(j.Context.InfluencePool,Is.EqualTo(7));
            j.RecruitSiteUnit(offer);Assert.That(j.Context.InfluencePool,Is.Zero);
            var golem=p.Units.Single();Assert.That(golem.Card,Is.SameAs(offer));
            j.EndTurn();j.BeginTurn();

            var ice=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_004");
            var b=j.BeginBattle(new[]{ice});b.Advance();b.Advance();
            b.UseSkill(skills[0],0);Assert.That(j.Context.IgnoreFireIceDamage,Is.EqualTo(2));
            b.Advance();Assert.That(p.Wounds,Is.Zero);Assert.That(j.Context.IgnoreFireIceDamage,Is.Zero);
            b.UseUnit(golem,0);b.Play(Card("advanced_card_000"),"strong");b.Attack(new[]{0});
            Assert.That(b.Context.DefeatedEnemyIndices.Contains(0),Is.True);b.Advance();j.EndTurn();
            Assert.That(j.Player,Is.SameAs(p));Assert.That(p.Units.Single(),Is.SameAs(golem));
            Assert.That(golem.IsReady,Is.False);Assert.That(golem.Wounds,Is.Zero);
            Assert.That(p.Mana.Crystals[ ManaColor.Red ],Is.Zero);Assert.That(p.Wounds,Is.Zero);
            Assert.That(j.CompletedTurns,Is.EqualTo(3));Assert.That(j.CompletedBattles,Is.EqualTo(2));
            Assert.That(p.Fame,Is.EqualTo(47));Assert.That(p.Position,Is.EqualTo(tower));
        }
    }
}
