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
    public sealed class PveArytheaSocialPrimaryJourneyTests
    {
        [Test]
        public void RedInspirationPaysOriginalAttackThenSecretNegotiationHealsActualBattleWound()
        {
            CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
            var originals = CardJsonLoader.LoadBasicActions();
            var player = OriginalHeroes.Get("Arythea").CreatePlayer(originals, new Random(0));
            player.Fame = 35;
            player.Armor = 3;
            player.Mana.Crystals[ManaColor.Red] = 1;
            player.Mana.Crystals[ManaColor.Blue] = 1;
            var skills = CardJsonLoader.LoadHeroSkills("hero_skill_pool_000");
            HeroSkillAcquisition.Acquire(player, skills[3]);
            HeroSkillAcquisition.Acquire(player, skills[8]);
            var cards = player.Deck.DrawPile.ToArray();
            var map = new MapState();
            var village = new AxialCoord(0, 1);
            foreach (var pos in new[] { new AxialCoord(0, 0), village })
                map.Placed.Add(pos, new MapTile(TileSet.Countryside, 0,
                    Enumerable.Repeat(TerrainType.Plains, 6).ToArray()));
            map.Places[village] = new PlaceState(PlaceType.Village);
            var j = new PveJourneySession(player, originals, map: map,
                manaRandom: new Random(0), supplyRandom: new Random(0));
            j.PrepareRound();
            j.SelectTactic("day_1");
            j.BeginTurn();
            var initial = player.Deck.Hand.ToArray();
            var drawn = player.Deck.DrawPile.Take(2).ToArray();
            j.UseOutsideSkill(skills[8]);
            Assert.That(drawn.All(c => player.Deck.Hand.Any(h => ReferenceEquals(h, c))), Is.True);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            var battle = j.BeginBattle(new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032") });
            battle.Advance(); battle.Advance(); battle.Advance();
            var wounds = player.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            Assert.That(wounds.Length, Is.EqualTo(2));
            var fury = initial.Single(c => c.Id == "basic_card_024");
            battle.Play(fury, "strong_attack");
            Assert.That(battle.Context.MeleePool, Is.EqualTo(4));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            battle.Attack(0); battle.Advance(); j.EndTurn();
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(9));
            Assert.That(player.Fame, Is.EqualTo(37));
            j.BeginTurn();
            var march = initial.Single(c => c.Id == "basic_card_000");
            j.PlayOutsideBattle(march, "base");
            j.Move(village);
            Assert.That(j.Context.MovementPool, Is.Zero);
            j.BeginVillageInteraction();
            j.UseOutsideSkill(skills[3]);
            Assert.That(j.Context.InfluencePool, Is.EqualTo(2));
            j.SidewaysInfluence(drawn[0]);
            Assert.That(j.Context.InfluencePool, Is.EqualTo(3));
            j.BuyVillageHealing();
            j.HealOutsideBattle();
            Assert.That(j.Context.InfluencePool, Is.Zero);
            Assert.That(player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, drawn[0])), Is.True);
            j.EndTurn();
            Assert.That(j.Player, Is.SameAs(player));
            Assert.That(j.CompletedTurns, Is.EqualTo(2));
            Assert.That(j.CompletedBattles, Is.EqualTo(1));
            Assert.That(player.Position, Is.EqualTo(village));
            Assert.That(player.Deck.DrawPileCount, Is.EqualTo(8));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(6));
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(3));
            var final = player.Deck.Hand.Concat(player.Deck.DrawPile).Concat(player.Deck.DiscardPile).ToArray();
            Assert.That(final.Length, Is.EqualTo(17));
            Assert.That(cards.All(c => final.Any(f => ReferenceEquals(c, f))), Is.True);
            Assert.That(wounds.Count(w => final.Any(f => ReferenceEquals(w, f))), Is.EqualTo(1));
        }
    }
}
