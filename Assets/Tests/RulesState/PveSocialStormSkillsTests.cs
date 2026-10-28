using System;
using System.Collections.Generic;
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
    public sealed class PveSocialStormSkillsTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static PveJourneySession Start(string pool, int index, PlaceType place = PlaceType.Village)
        {
            var p = new PlayerState { Fame = 3, HeroId = pool == "hero_skill_pool_000" ? "Arythea" : pool == "hero_skill_pool_001" ? "Braevalar" : pool == "hero_skill_pool_002" ? "Norowas" : "Goldyx" };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills(pool)[index]);
            p.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            var map = new MapState();
            map.Placed[new AxialCoord(0, 0)] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            map.Places[new AxialCoord(0, 0)] = new PlaceState(place);
            var j = new PveJourneySession(p, CardJsonLoader.LoadBasicActions(), map: map);
            j.SelectTactic("day_1"); j.BeginTurn(); return j;
        }

        [TestCase("000", 3, DayPart.Day, 2)] [TestCase("000", 3, DayPart.Night, 3)]
        [TestCase("002", 3, DayPart.Day, 3)] [TestCase("002", 3, DayPart.Night, 2)]
        public void NegotiationUsesPrintedDayNightInfluenceForActualVillageHealing(string pool, int index, DayPart day, int amount)
        {
            var j = Start("hero_skill_pool_" + pool, index); j.Context.DayPart = day;
            var skill = j.Player.Skills.Single(); j.Player.Deck.GainWound(); j.BeginVillageInteraction();
            Assert.That(j.OutsideSkillChoices(skill).Select(c => c.Option), Is.EqualTo(new[] { 0 }));
            j.UseOutsideSkill(skill); Assert.That(j.Context.InfluencePool, Is.EqualTo(amount));
            if (amount == 2)
            {
                var card = new DeedCard("basic_card_000", CardType.Action); j.Player.Deck.Hand.Add(card); j.SidewaysInfluence(card);
            }
            j.BuyVillageHealing(); j.HealOutsideBattle();
            Assert.That(j.Player.Wounds, Is.Zero); Assert.That(j.Context.InfluencePool, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => j.UseOutsideSkill(skill));
        }

        [TestCase(PlaceType.Village, 3)] [TestCase(PlaceType.Monastery, 3)]
        [TestCase(PlaceType.Keep, 2)] [TestCase(PlaceType.MageTower, 2)]
        [TestCase(PlaceType.City, 2)] [TestCase(PlaceType.MagicalGlade, 4)]
        public void TranceReadsActualCurrentMapSite(PlaceType site, int amount)
        {
            var j = Start("hero_skill_pool_001", 4, site); var skill = j.Player.Skills.Single();
            Assert.That(j.OutsideSkillChoices(skill).Select(c => c.Option), Is.EqualTo(new[] { 0 }));
            j.UseOutsideSkill(skill); Assert.That(j.Context.InfluencePool, Is.EqualTo(amount));
            if (site == PlaceType.Village)
            {
                j.Player.Deck.GainWound(); j.BeginVillageInteraction(); j.BuyVillageHealing(); j.HealOutsideBattle();
                Assert.That(j.Player.Wounds, Is.Zero); Assert.That(j.Context.InfluencePool, Is.Zero);
            }
        }

        [TestCase(2, 0, ManaColor.Green, ManaColor.Green)]
        [TestCase(2, 1, ManaColor.Blue, ManaColor.Green)]
        [TestCase(2, 256, ManaColor.Green, ManaColor.White)]
        [TestCase(2, 257, ManaColor.Blue, ManaColor.White)]
        [TestCase(3, 0, ManaColor.Blue, ManaColor.Blue)]
        [TestCase(3, 1, ManaColor.Green, ManaColor.Blue)]
        [TestCase(3, 256, ManaColor.Blue, ManaColor.Red)]
        [TestCase(3, 257, ManaColor.Green, ManaColor.Red)]
        public void StormIndependentChoicesPayTwoActualOriginalEnhancedCards(int index, int option, ManaColor first, ManaColor second)
        {
            var j = Start("hero_skill_pool_001", index); var p = j.Player; var skill = p.Skills.Single();
            Assert.That(j.OutsideSkillChoices(skill).Select(c => c.Option), Is.EquivalentTo(new[] { 0, 1, 256, 257 }));
            j.UseOutsideSkill(skill, option);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(first), Is.EqualTo(first == second ? 2 : 1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(second), Is.EqualTo(first == second ? 2 : 1));
            int movement = 0, influence = 0;
            foreach (var color in new[] { first, second })
            {
                string id = color == ManaColor.Green ? "basic_card_000" : color == ManaColor.Blue ? "basic_card_014" : color == ManaColor.White ? "basic_card_008" : "basic_card_022";
                var card = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(card); j.PlayOutsideBattle(card, "strong");
                Assert.That(p.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
                if (color == ManaColor.Green || color == ManaColor.Blue) movement += 4;
                else influence += color == ManaColor.White ? 4 : 5;
            }
            Assert.That(p.Mana.GetTotal(), Is.Zero); Assert.That(j.Context.MovementPool, Is.EqualTo(movement));
            Assert.That(j.Context.InfluencePool, Is.EqualTo(influence)); Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => j.UseOutsideSkill(skill, option));
        }

        [TestCase(2, -1)] [TestCase(2, 2)] [TestCase(3, 512)] [TestCase(3, 65536)]
        public void InvalidStormColorEncodingDoesNotGenerateManaOrConsumeUsage(int index, int option)
        {
            var j = Start("hero_skill_pool_001", index); var skill = j.Player.Skills.Single();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(j.Player, j.Context, skill, option));
            Assert.That(j.Player.Mana.GetTotal(), Is.Zero); Assert.That(SkillActions.IsUsed(j.Player, skill), Is.False);
        }

        [Test] public void CrystalLuckCountsInventoryColorsOnlyAndRealInteractionConsumesAndClearsInfluence()
        {
            var j = Start("hero_skill_pool_005", 5); var p = j.Player; var skill = p.Skills.Single();
            Assert.That(j.OutsideSkillChoices(skill).Select(c => c.Option), Is.EqualTo(new[] { 0 }));
            p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Green); p.Mana.AddCrystal(ManaColor.Blue);
            p.Mana.AddToken(ManaColor.White); p.Deck.GainWound();
            Assert.Throws<InvalidOperationException>(() => j.UseOutsideSkill(skill));
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); Assert.That(j.Context.InfluencePool, Is.Zero);
            j.BeginVillageInteraction();
            var extra = new DeedCard("basic_card_000", CardType.Action); p.Deck.Hand.Add(extra); j.SidewaysInfluence(extra);
            j.UseOutsideSkill(skill); Assert.That(j.Context.InfluencePool, Is.EqualTo(4));
            j.BuyVillageHealing(); j.HealOutsideBattle(); Assert.That(p.Wounds, Is.Zero);
            Assert.That(j.Context.InfluencePool, Is.EqualTo(1)); j.EndVillageInteraction();
            Assert.That(j.Context.InfluencePool, Is.Zero); Assert.That(p.Mana.Crystals.Values.Sum(), Is.EqualTo(4));
        }
    }
}
