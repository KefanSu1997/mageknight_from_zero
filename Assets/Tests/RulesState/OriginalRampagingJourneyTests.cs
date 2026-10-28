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
    public sealed class OriginalRampagingJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static readonly AxialCoord EnemyPosition = new(1, 0);
        private static readonly AxialCoord Flank = new(0, 1);

        private static PveJourneySession Create()
        {
            var player = new PlayerState();
            player.Deck.SetDeck(new[] { "basic_card_000", "basic_card_016", "basic_card_021", "basic_card_007", "basic_card_014" }
                .Select(id => new DeedCard(id, CardType.Action)));
            player.Mana.AddCrystal(ManaColor.Red, 1);
            player.Mana.AddCrystal(ManaColor.Blue, 1);
            var token = MonsterJsonLoader.ParseMonsters(File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(), "resources/text_json/monster.json"))).Single(m => m.Id == "monster_032");
            var supply = new EnemyTokenSupply(new[] { token });
            var map = new MapState();
            foreach (var hex in new[] { new AxialCoord(0, 0), EnemyPosition, Flank, new AxialCoord(-1, 0) })
                map.Placed.Add(hex, new MapTile(TileSet.Countryside, 1, Enumerable.Repeat(TerrainType.Plains, 6).ToArray()));
            var site = new PlaceState(PlaceType.OrcRampaging) { Enemy = supply.Draw(token.TokenColor) };
            map.Places.Add(EnemyPosition, site);
            map.RevealedSites.Add(site);
            var journey = new PveJourneySession(player, CardJsonLoader.LoadBasicActions(), map: map, enemySupply: supply);
            journey.PrepareRound(); journey.SelectTactic("day_1"); journey.BeginTurn();
            return journey;
        }

        private static DeedCard Card(PveJourneySession s, string id) => s.Player.Deck.Hand.Single(c => c.Id == id);

        [TestCase(false)]
        [TestCase(true)]
        public void ChallengeAndProvokedBattleUseActualTokenThenSettleSamePlayer(bool provoke)
        {
            var s = Create(); var player = s.Player; var context = s.Context;
            var site = s.Game.Map.Places[EnemyPosition]; var enemy = site.Enemy;
            if (provoke)
            {
                s.PlayOutsideBattle(Card(s, "basic_card_000"), "base");
                s.Move(Flank);
            }
            else s.ChallengeRampaging(EnemyPosition);
            var battle = s.Battle;
            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.Player, Is.SameAs(player));
            Assert.That(battle.Context, Is.SameAs(context));
            Assert.That(battle.Enemies[0], Is.SameAs(enemy));
            Assert.That(context.RampagingBattle, Is.Not.Null);
            Assert.That(battle.Phase, Is.EqualTo(Phase.Ranged));
            Assert.That(context.TurnActionCommitted, Is.True);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(player.Position, Is.EqualTo(provoke ? Flank : new AxialCoord(0, 0)));
            Assert.Throws<InvalidOperationException>(() => s.EndTurn());
            battle.Advance(); battle.Play(Card(s, "basic_card_016"), "strong"); battle.Block(0);
            battle.Advance(); battle.Advance(); battle.Play(Card(s, "basic_card_021"), "strong"); battle.Attack(0); battle.Advance();
            Assert.That(battle.Won, Is.True);
            Assert.That(site.Enemy, Is.Null);
            Assert.That(player.Reputation, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(enemy.Fame));
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(s.EnemySupply.IsInUse(enemy), Is.False);
            Assert.That(s.EnemySupply.DiscardPile.Count(t => ReferenceEquals(t, enemy)), Is.EqualTo(1));
            Assert.That(context.RampagingBattle, Is.Null);
            s.EndTurn();
            Assert.That(s.CompletedBattles, Is.EqualTo(1));
            Assert.That(s.CompletedTurns, Is.EqualTo(1));
            Assert.That(s.IsDefeated(enemy), Is.True);
            Assert.Throws<InvalidOperationException>(() => battle.Advance());
            s.BeginTurn();
            Assert.That(s.Player, Is.SameAs(player));
            Assert.That(s.Game.Map.Places[EnemyPosition], Is.SameAs(site));
            Assert.That(site.Enemy, Is.Null);
        }

        [TestCase(PlaceType.OrcRampaging)]
        [TestCase(PlaceType.Dragon)]
        public void OccupiedRampagingHexRejectsBeforeMovementFee(PlaceType kind)
        {
            var s = Create();
            var enemy = s.Game.Map.Places[EnemyPosition].Enemy;
            s.Game.Map.Places[EnemyPosition] = new PlaceState(kind) { Enemy = enemy };
            s.PlayOutsideBattle(Card(s, "basic_card_000"), "base");
            int pool = s.Context.MovementPool;
            Assert.Throws<InvalidOperationException>(() => s.Move(EnemyPosition));
            Assert.That(s.Context.MovementPool, Is.EqualTo(pool));
            Assert.That(s.Player.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(s.Context.InBattle, Is.False);
            Assert.That(s.Battle, Is.Null);
            Assert.That(s.EnemySupply.IsInUse(enemy), Is.True);
        }

        [Test] public void NonAdjacentAndForeignTokenChallengeRejectWithoutCommittingAction()
        {
            var s = Create(); var site = s.Game.Map.Places[EnemyPosition]; var actual = site.Enemy;
            Assert.Throws<InvalidOperationException>(() => s.ChallengeRampaging(new AxialCoord(5, 5)));
            site.Enemy = actual with { };
            Assert.Throws<InvalidOperationException>(() => s.ChallengeRampaging(EnemyPosition));
            Assert.That(s.Context.TurnActionCommitted, Is.False);
            Assert.That(s.Context.InBattle, Is.False);
            Assert.That(s.Context.RampagingBattle, Is.Null);
            Assert.That(s.Battle, Is.Null);
            Assert.That(s.EnemySupply.IsInUse(actual), Is.True);
        }
    }
}
