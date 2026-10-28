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
    public sealed class PveGoldyxPrimaryConsumersJourneyTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void GreenInspirationOriginalDrawThenFreeFlightAndCrystalLuckHealActualBattleWound()
        {
            var originals = CardJsonLoader.LoadBasicActions();
            var player = OriginalHeroes.Get("Goldyx").CreatePlayer(originals, new Random(0));
            player.Fame = 35;
            player.Armor = 2 + (player.Level + 1) / 4;
            player.Mana.Crystals[ManaColor.Red] = 1;
            player.Mana.Crystals[ManaColor.Blue] = 1;
            var pool = CardJsonLoader.LoadHeroSkills("hero_skill_pool_005");
            var luck = pool[5]; var flight = pool[6]; var inspiration = pool[8];
            foreach (var skill in new[] { luck, flight, inspiration })
                HeroSkillAcquisition.Acquire(player, skill);
            Assert.That(player.Level, Is.EqualTo(6));
            Assert.That(player.Skills.Count, Is.EqualTo(player.Level / 2));
            var originalInstances = player.Deck.DrawPile.ToArray();
            var origin = new AxialCoord(0, 0);
            var forest = new AxialCoord(1, 0);
            var village = new AxialCoord(0, 1);
            var map = new MapState();
            foreach (var pos in new[] { origin, forest, village })
                map.Placed.Add(pos, new MapTile(TileSet.Countryside, 0,
                    Enumerable.Repeat(pos.Equals(forest) ? TerrainType.Forest : TerrainType.Plains, 6).ToArray()));
            map.Places.Add(village, new PlaceState(PlaceType.Village));
            var journey = new PveJourneySession(player, originals, map: map,
                manaRandom: new Random(0), supplyRandom: new Random(0));
            journey.PrepareRound();
            Assert.That(player.Deck.Hand.Select(c => c.Id), Is.EqualTo(new[] {
                "basic_card_021", "basic_card_014", "basic_card_000",
                "basic_card_007", "basic_card_023", "basic_card_009" }));
            journey.SelectTactic("day_1"); journey.BeginTurn();
            var fury = player.Deck.Hand.Single(c => c.Id == "basic_card_021");
            var stamina = player.Deck.Hand.Single(c => c.Id == "basic_card_014");
            var march = player.Deck.Hand.Single(c => c.Id == "basic_card_000");
            var battle = journey.BeginBattle(new[] {
                CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") });
            battle.Advance(); battle.Advance(); battle.Advance();
            var wounds = player.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            Assert.That(wounds.Length, Is.EqualTo(2));
            battle.Play(fury, "attack"); battle.PlaySideways(stamina);
            battle.Attack(0); battle.Advance(); journey.EndTurn();
            Assert.That(player.Fame, Is.EqualTo(37));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(10));
            Assert.That(wounds.All(w => player.Deck.Hand.Any(c => ReferenceEquals(c, w))), Is.True);

            journey.BeginTurn();
            var drawn = player.Deck.DrawPile.Take(2).ToArray();
            Assert.That(drawn.Select(c => c.Id), Is.EqualTo(new[] {
                "basic_card_021", "basic_card_006" }));
            journey.UseOutsideSkill(inspiration);
            Assert.That(drawn.All(c => player.Deck.Hand.Any(h => ReferenceEquals(h, c))), Is.True);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            journey.PlayOutsideBattle(march, "strong");
            Assert.That(journey.Context.MovementPool, Is.EqualTo(4));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            journey.PlayOutsideBattle(drawn[1], "green_crystal");
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, drawn[1])), Is.True);
            Assert.That(player.Mana.Crystals.Count(kv => kv.Value > 0), Is.EqualTo(3));
            Assert.That(player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1));
            journey.Move(forest);
            Assert.That(player.Position, Is.EqualTo(forest));
            Assert.That(journey.Context.MovementPool, Is.EqualTo(1));
            Assert.That(journey.MovementCost(village), Is.EqualTo(2));
            journey.Fly(flight, village);
            Assert.That(player.Position, Is.EqualTo(village));
            Assert.That(journey.Context.MovementPool, Is.EqualTo(1), "One actual hex is free");
            Assert.That(SkillActions.IsUsed(player, flight), Is.True);
            journey.BeginVillageInteraction();
            Assert.That(journey.Context.InfluencePool, Is.Zero);
            journey.UseOutsideSkill(luck);
            Assert.That(journey.Context.InfluencePool, Is.EqualTo(3));
            Assert.That(new[] { luck, flight, inspiration }.All(s => SkillActions.IsUsed(player, s)), Is.True);
            journey.BuyVillageHealing();
            Assert.That(journey.Context.InfluencePool, Is.Zero);
            Assert.That(journey.Context.Healing.Total, Is.EqualTo(1));
            journey.HealOutsideBattle();
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(journey.Context.Healing.Total, Is.Zero);
            Assert.That(player.Deck.Hand.Count(c => wounds.Any(w => ReferenceEquals(c, w))), Is.EqualTo(1));
            Assert.That(player.Deck.DiscardPile.Count(c => c.Type == CardType.Wound), Is.Zero);
            journey.EndTurn();

            Assert.That(journey.Player, Is.SameAs(player));
            Assert.That(journey.CompletedTurns, Is.EqualTo(2));
            Assert.That(journey.CompletedBattles, Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(7));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(4));
            var finalCards = player.Deck.Hand.Concat(player.Deck.DrawPile)
                .Concat(player.Deck.DiscardPile).ToArray();
            Assert.That(finalCards.Length, Is.EqualTo(17));
            Assert.That(originalInstances.All(c => finalCards.Any(x => ReferenceEquals(x, c))), Is.True);
            Assert.That(finalCards.Count(c => wounds.Any(w => ReferenceEquals(w, c))), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            Assert.That(player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1));
            Assert.That(player.Position, Is.EqualTo(village));
        }
    }
}
