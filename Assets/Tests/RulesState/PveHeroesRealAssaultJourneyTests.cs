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
    public sealed class PveHeroesRealAssaultJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void ActualTowerTunnelKeepsAssaultFeeThenSameHeroesAndRewardSurviveFormalTurn()
        {
            var player = new PlayerState { HeroId = "Tovak", Fame = 15 };
            player.Mana.AddCrystal(ManaColor.Red); player.Mana.AddCrystal(ManaColor.Blue);
            var hero = new UnitState(CardJsonLoader.LoadUnit("army_011")); player.Units.Add(hero);
            Assert.That(player.FreeSlots, Is.EqualTo(1), "等级4两槽中的一支正式部队");
            player.Deck.SetDeck(new[] { "basic_card_008", "basic_card_000", "magic_004", "items_007", "items_015" }
                .Select(id => new DeedCard(id, id.StartsWith("items_") ? CardType.Artifact : id.StartsWith("magic_") ? CardType.Spell : CardType.Action)));
            var origin = new AxialCoord(0, 0); var mountain = new AxialCoord(0, -1); var target = new AxialCoord(-1, -1);
            var map = new MapState();
            map.Placed[origin] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            map.Placed[mountain] = new MapTile(TileSet.Countryside, 1, new[] { TerrainType.Mountain });
            map.Placed[target] = new MapTile(TileSet.Countryside, 2, new[] { TerrainType.Plains });
            var supply = new EnemyTokenSupply(new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_002") }, new Random(0));
            var guard = supply.Draw(EnemyTokenSupply.GuardColor(PlaceType.MageTower));
            var site = new PlaceState(PlaceType.MageTower) { Enemy = guard }; map.Places[target] = site;
            var spells = CardJsonLoader.LoadSpells().ToArray();
            var cards = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .Concat(spells.Select(s => new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost)));
            var j = new PveJourneySession(player, cards, CardJsonLoader.LoadItems(), map: map, spells: spells,
                enemySupply: supply, manaRandom: new Random(0), tacticRandom: new Random(0), supplyRandom: new Random(0));
            j.SelectTactic("day_1"); j.BeginTurn();
            DeedCard Card(string id) => player.Deck.Hand.Single(c => c.Id == id);
            j.PlayOutsideArtifact(Card("items_007"), false);
            j.PlayOutsideArtifact(Card("items_015"), true, null, ManaColor.Black, ManaColor.White, ManaColor.White);
            j.PlayOutsideWithInputs(Card("magic_004"), "strong", new PveDeedSelection { TravelPath = new[] { mountain, target } });
            Assert.That(j.Context.CombatSite, Is.SameAs(site));
            Assert.That(j.Battle.Enemies.Single(), Is.SameAs(guard));
            Assert.That(player.Position, Is.EqualTo(target));
            Assert.That(j.Context.IgnoreSiteFortifications, Is.True);
            Assert.That(CardUnitActions.ActivationInfluenceCost(j.Context, hero), Is.EqualTo(2));
            Assert.That(player.Mana.Tokens[ManaColor.White], Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => j.Battle.UseUnit(hero, 1));
            Assert.That(hero.IsReady, Is.True); Assert.That(player.Mana.Tokens[ManaColor.White], Is.EqualTo(2));
            j.Battle.Play(Card("basic_card_008"), "base");
            Assert.That(j.Context.InfluencePool, Is.EqualTo(2));
            j.Battle.UseUnit(hero, 1);
            Assert.That(j.Context.InfluencePool, Is.Zero); Assert.That(player.Mana.Tokens[ManaColor.White], Is.EqualTo(1));
            Assert.That(j.Context.RangedPool, Is.EqualTo(7)); Assert.That(hero.IsReady, Is.False);
            Assert.That(j.Battle.Attack(0).Kills, Is.EqualTo(1));
            while (j.Battle.Phase != Phase.End) j.Battle.Advance();
            j.FinishTunnelAssault(false);
            Assert.That(site.Conquered, Is.True); Assert.That(site.OwnerId, Is.EqualTo(player.Id));
            Assert.That(supply.DiscardPile.Single(), Is.SameAs(guard));
            var reward = j.Game.Globals.Spells.Offer[0]; j.SelectSiteSpellReward(reward);
            j.EndTurn();
            Assert.That(player.Fame, Is.EqualTo(20)); Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand.Any(c => c.Id == reward.Id), Is.True);
            Assert.That(j.CompletedTurns, Is.EqualTo(1)); Assert.That(j.CompletedBattles, Is.EqualTo(1));
            j.BeginTurn();
            Assert.That(j.Player, Is.SameAs(player)); Assert.That(player.Units.Single(), Is.SameAs(hero));
            Assert.That(hero.IsReady, Is.False); Assert.That(player.Position, Is.EqualTo(target));
            Assert.That(j.Context.InfluencePool, Is.Zero);
        }
    }
}
