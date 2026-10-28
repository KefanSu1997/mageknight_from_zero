using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveFamiliarBaseInfluenceJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        // Read the existing scene's original catalog order, never replace its supply or fixture.
        private static string Section(string yaml, string name) => Regex.Match(yaml,
            @"(?m)^  " + name + @":\r?\n((?:  .*(?:\r?\n|$))*)").Groups[1].Value;

        [Test]
        public void OriginalGladeTranceRecruitsRedFamiliarThenBaseInfluenceHealsRealPreviousBattleWound()
        {
            var yaml = File.ReadAllText("Assets/Scenes/Part1/Part1_PveBattle.unity");
            var unitMeta = Directory.GetFiles("Assets/GameData/BattleDemo/Units", "*.asset.meta")
                .ToDictionary(p => Regex.Match(File.ReadAllText(p), @"(?m)^guid: (\w+)").Groups[1].Value,
                    p => Path.GetFileName(p).Replace(".asset.meta", ""));
            var units = Regex.Matches(Section(yaml, "unitCatalog"), @"guid: (\w+)")
                .Cast<Match>().Select(m => CardJsonLoader.LoadUnit(unitMeta[m.Groups[1].Value])).ToArray();
            var monsters = CardJsonLoader.LoadMonsters();
            var tokens = Regex.Matches(Section(yaml, "worldEnemyCatalog"), @"originalId: (\w+)")
                .Cast<Match>().Select(m => monsters.Single(v => v.Id == m.Groups[1].Value)).ToArray();
            Assert.That(units.Length, Is.EqualTo(30)); Assert.That(tokens.Length, Is.GreaterThan(1));
            var enemySupply = new EnemyTokenSupply(tokens);
            var map = new MapState();
            OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map, enemySupply);
            var player = new PlayerState { HeroId = "Braevalar", Fame = 35, Armor = 3 };
            player.Mana.AddCrystal(ManaColor.Red); player.Mana.AddCrystal(ManaColor.Blue);
            var trance = CardJsonLoader.LoadHeroSkills("hero_skill_pool_001")[4];
            HeroSkillAcquisition.Acquire(player, trance);
            player.Deck.SetDeck(new[] { "basic_card_000", "basic_card_008", "basic_card_014", "advanced_card_018", "advanced_card_039" }
                .Select(id => new DeedCard(id, CardType.Action)));
            var j = new PveJourneySession(player, CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
                map: map, enemySupply: enemySupply, unitDeck: units);
            DeedCard Card(string id) => player.Deck.Hand.Single(c => c.Id == id);
            j.PrepareRound(); j.SelectTactic("day_1"); j.BeginTurn();
            foreach (var id in new[] { "basic_card_000", "basic_card_014", "advanced_card_039" }) j.PlayOutsideBattle(Card(id), "base");
            foreach (var pos in new[] { new AxialCoord(0,-1), new AxialCoord(1,-2), new AxialCoord(1,-3) }) j.Move(pos);
            Assert.That(j.Context.MovementPool, Is.Zero); j.EndTurn();
            for (var round = 2; round <= 5; round++)
            {
                j.AnnounceRoundEnd(); j.EndRound(); j.PrepareRound(); j.SelectTactic(round % 2 == 0 ? "night_1" : "day_1");
            }
            j.BeginTurn();
            var offer = j.Game.Globals.Units.Offer.Single(u => u.Id == "army_2_004");
            Assert.That(player.Position, Is.EqualTo(new AxialCoord(1,-3)));
            j.UseOutsideSkill(trance); Assert.That(j.Context.InfluencePool, Is.EqualTo(4));
            j.PlayOutsideBattle(Card("basic_card_008"), "base"); Assert.That(j.Context.InfluencePool, Is.EqualTo(6));
            j.RecruitGladeUnit(offer, ManaColor.Red);
            var familiar = player.Units.Single();
            Assert.That(familiar.Card, Is.SameAs(offer)); Assert.That(familiar.AttunementColor, Is.EqualTo(ManaColor.Red));
            Assert.That(j.Context.InfluencePool, Is.Zero); Assert.That(player.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            j.EndTurn(); j.BeginTurn();
            var enemy = map.Places[new AxialCoord(1,-4)].Enemy;
            j.ChallengeRampaging(new AxialCoord(1,-4)); Assert.That(j.Battle.Enemies.Single(), Is.SameAs(enemy));
            j.Battle.Advance(); j.Battle.Advance(); j.Battle.Advance();
            Assert.That(player.Wounds, Is.EqualTo(2)); Assert.That(familiar.IsReady, Is.True);
            foreach (var id in new[] { "basic_card_014", "advanced_card_018", "advanced_card_039" }) j.Battle.PlaySideways(Card(id));
            Assert.That(j.Context.MeleePool, Is.EqualTo(3)); Assert.That(j.Battle.Attack(0).Kills, Is.EqualTo(1));
            j.Battle.Advance(); Assert.That(player.Fame, Is.EqualTo(37)); Assert.That(player.Reputation, Is.EqualTo(1));
            var wounds = player.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            j.EndTurn(siteSelection: new SiteTurnSelection { GladeWound = wounds[0] });
            Assert.That(player.Wounds, Is.EqualTo(1)); Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, wounds[1])), Is.True);
            j.BeginTurn(); Assert.That(player.Units.Single(), Is.SameAs(familiar)); Assert.That(familiar.IsReady, Is.True);
            j.PlayOutsideBattle(Card("basic_card_000"), "base"); j.Move(new AxialCoord(2,-3));
            j.UseOutsideUnit(familiar, 2, option: 1);
            Assert.That(j.Context.InfluencePool, Is.EqualTo(3)); Assert.That(familiar.IsReady, Is.False);
            Assert.That(familiar.AttunementColor, Is.EqualTo(ManaColor.Red));
            j.BeginVillageInteraction(); Assert.That(j.Context.InfluencePool, Is.EqualTo(3));
            j.BuyVillageHealing(); Assert.That(j.Context.InfluencePool, Is.Zero); Assert.That(j.Context.Healing.Total, Is.EqualTo(1));
            j.HealOutsideBattle(); Assert.That(player.Wounds, Is.Zero); Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, wounds[1])), Is.False);
            Assert.That(j.Context.Healing.Total, Is.Zero); j.EndTurn();
            Assert.That(j.Player, Is.SameAs(player)); Assert.That(player.Units.Single(), Is.SameAs(familiar));
            Assert.That(j.CompletedTurns, Is.EqualTo(4)); Assert.That(j.CompletedBattles, Is.EqualTo(1));
            Assert.That(familiar.IsReady, Is.False); Assert.That(player.Position, Is.EqualTo(new AxialCoord(2,-3)));
        }
    }
}

