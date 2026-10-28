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
    public sealed class PveChaosGoldPrimaryJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test] public void SeededOwnChaosWhiteRecruitRedAttackThenGoldColdFireRangedConsumer()
        {
            var p = new PlayerState { HeroId = "Krang", Fame = 35, Armor = 3 };
            p.Mana.Crystals[ManaColor.Red] = 1;
            p.Mana.Crystals[ManaColor.Blue] = 1;
            var skill = CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[7];
            HeroSkillAcquisition.Acquire(p, skill, new Random(0));
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Gold));
            Assert.That(p.Level, Is.EqualTo(6));
            Assert.That(p.Skills.Count, Is.LessThanOrEqualTo(p.Level / 2));
            foreach (var id in new[] { "basic_card_000", "basic_card_001", "basic_card_008",
                "basic_card_014", "basic_card_021", "advanced_card_000" })
                p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            var originalCards = p.Deck.Hand.ToArray();
            DeedCard Card(string id) => p.Deck.Hand.Single(c => c.Id == id);
            var origin = new AxialCoord(0, 0); var village = new AxialCoord(0, 1);
            var map = new MapState();
            foreach (var hex in new[] { origin, village })
                map.Placed.Add(hex, new MapTile(TileSet.Countryside, 0,
                    Enumerable.Repeat(TerrainType.Plains, 6).ToArray()));
            map.Places.Add(village, new PlaceState(PlaceType.Village));
            var offered = CardJsonLoader.LoadUnit("army_2_006");
            var j = new PveJourneySession(p,
                CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
                map: map, unitDeck: new[] { offered }, manaRandom: new Random(0), supplyRandom: new Random(0));
            j.PrepareRound(); j.SelectTactic("day_1"); j.BeginTurn();
            j.PlayOutsideBattle(Card("basic_card_000"), "base"); j.Move(village);
            j.EndTurn();
            Assert.That(p.ChaosUnusedRotationPending, Is.True);
            ChaosSkillRules.ResolveUnusedRotation(p, true);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Blue));
            j.BeginTurn(); j.Rest(Card("basic_card_001")); j.EndTurn();
            ChaosSkillRules.ResolveUnusedRotation(p, true);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Green));
            j.BeginTurn(); j.Rest(Card("basic_card_014")); j.EndTurn();
            ChaosSkillRules.ResolveUnusedRotation(p, true);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Black));
            Assert.That(j.CompletedTurns, Is.EqualTo(3));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(3));
            j.BeginTurn();
            j.PlayOutsideBattle(Card("basic_card_008"), "base");
            j.UseOutsideSkill(skill, 0);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.White));
            Assert.That(j.Context.InfluencePool, Is.EqualTo(4));
            j.BeginVillageInteraction(); j.RecruitVillageUnit(offered);
            var farmer = p.Units.Single();
            Assert.That(farmer.Card, Is.SameAs(offered));
            Assert.That(j.Context.InfluencePool, Is.Zero);
            j.EndVillageInteraction(); j.EndTurn();
            Assert.That(p.ChaosUnusedRotationPending, Is.False);

            j.BeginTurn();
            var skeleton = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var first = j.BeginBattle(new[] { skeleton });
            first.Advance(); first.Advance(); first.Advance();
            var wounds = p.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            Assert.That(wounds.Length, Is.EqualTo(2));
            first.UseSkill(skill, 0);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Red));
            first.Play(Card("basic_card_021"), "attack");
            first.Attack(0); first.Advance(); j.EndTurn();
            Assert.That(p.Fame, Is.EqualTo(37));
            Assert.That(p.ChaosUnusedRotationPending, Is.False);

            j.BeginTurn();
            var wolf = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_007");
            var second = j.BeginBattle(new[] { wolf });
            Assert.That(ChaosSkillRules.Options(p, second.Context).Select(o => o.Option), Does.Contain(2));
            second.UseSkill(skill, 2);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Gold));
            var reward = second.Context.CombatPower.Attacks.Single();
            Assert.That(reward.Type, Is.EqualTo(AttackType.Ranged));
            Assert.That(reward.Element, Is.EqualTo(Element.ColdFire));
            Assert.That(reward.Value, Is.EqualTo(1));
            Assert.That(SkillActions.IsUsed(p, skill), Is.True);
            second.Play(Card("advanced_card_000"), "strong");
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(second.Context.CombatPower.Attacks.Sum(a => a.Value), Is.EqualTo(4));
            Assert.That(second.Context.CombatPower.Attacks.All(a => a.Type == AttackType.Ranged), Is.True);
            second.Attack(0);
            Assert.That(second.Context.DefeatedEnemyIndices.Contains(0), Is.True);
            for (int n = 0; n < 4; n++) second.Advance();
            j.EndTurn();
            Assert.That(j.Player, Is.SameAs(p));
            Assert.That(p.Units.Single(), Is.SameAs(farmer));
            Assert.That(farmer.IsReady, Is.True);
            Assert.That(farmer.Wounds, Is.Zero);
            Assert.That(p.ChaosColor, Is.EqualTo(ManaColor.Gold));
            Assert.That(p.ChaosUnusedRotationPending, Is.False);
            Assert.That(j.CompletedTurns, Is.EqualTo(6));
            Assert.That(j.CompletedBattles, Is.EqualTo(2));
            Assert.That(p.Fame, Is.EqualTo(40));
            Assert.That(p.Wounds, Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(6));
            Assert.That(p.Deck.DrawPileCount, Is.Zero);
            Assert.That(wounds.All(w => p.Deck.Hand.Any(c => ReferenceEquals(c, w))), Is.True);
            var all = p.Deck.Hand.Concat(p.Deck.DiscardPile).ToArray();
            Assert.That(originalCards.All(c => all.Any(a => ReferenceEquals(a, c))), Is.True);
        }
    }
}
