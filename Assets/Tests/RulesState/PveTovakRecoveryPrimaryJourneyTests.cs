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
    public sealed class PveTovakRecoveryPrimaryJourneyTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [Test]
        public void ActualBattleWoundThenOwnFearlessPainAndInspirationPayOriginalMovement()
        {
            var originals = CardJsonLoader.LoadBasicActions();
            var player = OriginalHeroes.Get("Tovak").CreatePlayer(originals, new Random(0));
            player.Fame = 35;
            player.Armor = 2 + (player.Level + 1) / 4;
            player.Mana.Crystals[ManaColor.Red] = 1;
            player.Mana.Crystals[ManaColor.Blue] = 1;
            var pool = CardJsonLoader.LoadHeroSkills("hero_skill_pool_007");
            var fearless = pool[5];
            var inspiration = pool[8];
            HeroSkillAcquisition.Acquire(player, fearless);
            HeroSkillAcquisition.Acquire(player, inspiration);
            Assert.That(player.Level, Is.EqualTo(6));
            Assert.That(player.Armor, Is.EqualTo(3));
            Assert.That(player.Skills.Count, Is.LessThanOrEqualTo(player.Level / 2));
            var originalInstances = player.Deck.DrawPile.ToArray();
            var map = new MapState();
            var destination = new AxialCoord(0, 1);
            foreach (var pos in new[] { new AxialCoord(0, 0), destination })
                map.Placed.Add(pos, new MapTile(TileSet.Countryside, 0,
                    Enumerable.Repeat(TerrainType.Plains, 6).ToArray()));
            var journey = new PveJourneySession(player, originals, map: map,
                manaRandom: new Random(0), supplyRandom: new Random(0));
            journey.PrepareRound();
            Assert.That(player.Deck.Hand.Select(c => c.Id), Is.EqualTo(new[] {
                "basic_card_021", "basic_card_014", "basic_card_000",
                "basic_card_007", "basic_card_027", "basic_card_009" }));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(10));
            journey.SelectTactic("day_1");
            journey.BeginTurn();
            var initialFury = player.Deck.Hand.Single(c => c.Id == "basic_card_021");
            var march = player.Deck.Hand.Single(c => c.Id == "basic_card_000");
            var stamina = player.Deck.Hand.Single(c => c.Id == "basic_card_014");
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var battle = journey.BeginBattle(new[] { enemy });
            battle.Advance();
            battle.Advance();
            battle.Advance();
            var wounds = player.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            Assert.That(wounds.Length, Is.EqualTo(2), "Original attack4 versus level6 armor3");
            battle.Play(initialFury, "attack");
            battle.PlaySideways(march);
            Assert.That(battle.Context.MeleePool, Is.EqualTo(3));
            battle.Attack(0);
            battle.Advance();
            journey.EndTurn();
            Assert.That(journey.CompletedTurns, Is.EqualTo(1));
            Assert.That(journey.CompletedBattles, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(37));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(10));
            Assert.That(wounds.All(w => player.Deck.Hand.Any(c => ReferenceEquals(c, w))), Is.True);

            journey.BeginTurn();
            var fearlessDraw = player.Deck.DrawPile.First();
            Assert.That(fearlessDraw.Id, Is.EqualTo("basic_card_021"));
            Assert.That(fearlessDraw, Is.Not.SameAs(initialFury));
            journey.UseOutsideSkill(fearless);
            Assert.That(player.Deck.DiscardPile.Any(c => ReferenceEquals(c, wounds[0])), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, wounds[1])), Is.True);
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, fearlessDraw)), Is.True);
            Assert.That(player.Deck.RemovedCards.Count, Is.Zero);
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(9));
            Assert.That(SkillActions.IsUsed(player, fearless), Is.True);
            var inspired = player.Deck.DrawPile.Take(2).ToArray();
            Assert.That(inspired.Select(c => c.Id), Is.EqualTo(new[] {
                "basic_card_002", "basic_card_007" }));
            journey.UseOutsideSkill(inspiration);
            Assert.That(inspired.All(c => player.Deck.Hand.Any(h => ReferenceEquals(h, c))), Is.True);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(8));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(7));
            Assert.That(player.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(SkillActions.IsUsed(player, inspiration), Is.True);
            journey.PlayOutsideBattle(stamina, "strong");
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(journey.Context.MovementPool, Is.EqualTo(4));
            journey.SidewaysMovement(fearlessDraw);
            journey.SidewaysMovement(inspired[0]);
            Assert.That(journey.Context.MovementPool, Is.EqualTo(6));
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, fearlessDraw)), Is.True);
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, inspired[0])), Is.True);
            journey.Move(destination);
            Assert.That(journey.Context.MovementPool, Is.EqualTo(4));
            journey.EndTurn();

            Assert.That(journey.Player, Is.SameAs(player));
            Assert.That(journey.CompletedTurns, Is.EqualTo(2));
            Assert.That(journey.CompletedBattles, Is.EqualTo(1));
            Assert.That(player.Position, Is.EqualTo(destination));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(6));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(6));
            Assert.That(player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(1));
            Assert.That(player.Deck.DiscardPile.Count(c => c.Type == CardType.Wound), Is.EqualTo(1));
            Assert.That(player.Deck.RemovedCards.Count, Is.Zero);
            var finalCards = player.Deck.Hand.Concat(player.Deck.DrawPile)
                .Concat(player.Deck.DiscardPile).ToArray();
            Assert.That(finalCards.Length, Is.EqualTo(18));
            Assert.That(originalInstances.All(c => finalCards.Any(x => ReferenceEquals(x, c))), Is.True);
            Assert.That(wounds.All(c => finalCards.Any(x => ReferenceEquals(x, c))), Is.True);
        }
    }
}
