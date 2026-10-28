using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    // Original source instances, paid movement/exploration and real engine turn order.
    public sealed class SpaceBendingBasicStateTests
    {
        private sealed class QuietExploration : Random
        { public override int Next(int maxValue) => maxValue - 1; }
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Add(PlayerState p, string id)
        {
            var d = new DeedCard(id, id.StartsWith("magic_") ? CardType.Spell : CardType.Action);
            p.Deck.Hand.Add(d); return d;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard d, bool strong = false)
        {
            ActionCardData original;
            if (d.Type == CardType.Spell)
            {
                var s = CardJsonLoader.LoadSpells().Single(x => x.Id == d.Id);
                original = new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath,
                    s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost);
            }
            else original = CardJsonLoader.LoadBasicActions().Single(x => x.Id == d.Id);
            CardPlayActions.Play(p, c, d, original, strong);
        }
        private static MapTile Plain(int id) =>
            new MapTile(TileSet.Countryside, id, Enumerable.Repeat(TerrainType.Plains, 6).ToArray());
        private static MapState Map(params TerrainType[] terrains)
        {
            var map = new MapState();
            for (int q = 0; q < terrains.Length; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q,
                    Enumerable.Repeat(terrains[q], 6).ToArray());
            return map;
        }
        private static void Exact(IEnumerable<DeedCard> actual, params DeedCard[] expected)
        {
            var a = actual.ToArray(); Assert.That(a.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < a.Length; i++) Assert.That(a[i], Is.SameAs(expected[i]));
        }
        private static void Unique(PlayerState p, params DeedCard[] expected)
        {
            var all = p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.DrawPile)
                .Concat(p.Deck.DiscardPile).Concat(p.Deck.RemovedCards).Concat(p.Deck.RoundAsideCards).ToArray();
            Assert.That(all.Length, Is.EqualTo(expected.Length));
            foreach (var d in expected) Assert.That(all.Count(x => ReferenceEquals(x, d)), Is.EqualTo(1), d.Id);
        }
        private static void Move(PlayerState p, ActionContext c, int q, int expectedLeft)
        {
            var target = new AxialCoord(q, 0);
            Assert.That(MovementService.GetCost(target, c.MovementMap, c.DayPart, c), Is.EqualTo(2));
            Assert.That(new MovementService().TryMoveUsingPool(p, target, c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(target)); Assert.That(c.MovementPool, Is.EqualTo(expectedLeft));
        }

        [Test]
        public void BasePaysDestinationCostsForTwoLeapsAndPermissionExpiresNextTurn()
        {
            var p = new PlayerState();
            var map = Map(TerrainType.Plains, TerrainType.Mountain, TerrainType.Plains,
                TerrainType.Lake, TerrainType.Plains, TerrainType.Mountain, TerrainType.Plains);
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = map };
            var source = Add(p, "magic_016"); var march = Add(p, "basic_card_000");
            var nextMarch = Add(p, "basic_card_000");
            p.Mana.AddToken(ManaColor.Blue); p.Mana.AddToken(ManaColor.Green);
            Play(p, c, source);
            Assert.That(c.SpaceBendingActive, Is.True); Assert.That(c.MovementPool, Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Play(p, c, march, true); Assert.That(c.MovementPool, Is.EqualTo(4));
            Move(p, c, 2, 2); Move(p, c, 4, 0);
            Assert.That(map.Placed[new AxialCoord(1, 0)].Edges[0], Is.EqualTo(TerrainType.Mountain));
            Assert.That(map.Placed[new AxialCoord(3, 0)].Edges[0], Is.EqualTo(TerrainType.Lake));
            c.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, c.DayPart, null, c);
            Assert.That(c.SpaceBendingActive, Is.False); Assert.That(c.IgnoreRampaging, Is.False);
            Exact(p.Deck.Hand, nextMarch);
            new PlayerTurnEngine().StartTurn(p, c.DayPart); Play(p, c, nextMarch);
            Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(6, 0), map, c), Is.False);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(4, 0))); Assert.That(c.MovementPool, Is.EqualTo(2));
            Unique(p, source, march, nextMarch);
        }

        [Test]
        public void BaseExploresDistanceTwoThroughActualTileDeckAndPaysSharedMovementOnlyOnSuccess()
        {
            var p = new PlayerState(); var map = Map(TerrainType.Plains, TerrainType.Plains);
            var next = Plain(77); map.Countryside.Push(next);
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = map };
            var source = Add(p, "magic_016"); var march = Add(p, "basic_card_000");
            p.Mana.AddToken(ManaColor.Blue); Play(p, c, march);
            var exploration = new ExplorationService(map, new QuietExploration());
            var target = new AxialCoord(2, 0);
            var rejected = exploration.ExploreUsingPool(p, target, 0, c);
            Assert.That(rejected.Success, Is.False); Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.That(map.Countryside.Peek(), Is.SameAs(next)); Assert.That(map.Placed.ContainsKey(target), Is.False);
            Play(p, c, source);
            Assert.That(c.MovementPool, Is.EqualTo(2));
            var result = exploration.ExploreUsingPool(p, target, 0, c);
            Assert.That(result.Success, Is.True); Assert.That(result.RemainingMovement, Is.Zero);
            Assert.That(c.MovementPool, Is.Zero); Assert.That(result.Tile, Is.SameAs(next));
            Assert.That(map.Placed[target], Is.SameAs(next)); Assert.That(map.Countryside.Count, Is.Zero);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(result.CombatEvent, Is.Null);
            Unique(p, source, march);
        }

        [Test]
        public void MovementAvoidsActualRampageOnlyThisTurnThenSameMoveForcesOriginalEnemyCombat()
        {
            var p = new PlayerState(); var map = Map(TerrainType.Plains, TerrainType.Plains);
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = map };
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
            var site = new PlaceState(PlaceType.OrcRampaging) { Enemy = enemy };
            map.Places[new AxialCoord(0, 1)] = site;
            var source = Add(p, "magic_016"); var march = Add(p, "basic_card_000");
            var nextMarch = Add(p, "basic_card_000"); var fury = Add(p, "basic_card_021");
            p.Mana.AddToken(ManaColor.Blue); p.Mana.AddToken(ManaColor.Green);
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Green);
            Play(p, c, source); Play(p, c, march, true);
            Move(p, c, 1, 2); Move(p, c, 0, 0);
            Assert.That(c.InBattle, Is.False); Assert.That(c.RampagingBattle, Is.Null);
            Assert.That(site.Enemy, Is.SameAs(enemy));
            c.SkipDraw = true; new PlayerTurnEngine().EndTurn(p, c.DayPart, null, c);
            new PlayerTurnEngine().StartTurn(p, c.DayPart);
            Play(p, c, nextMarch, true); Move(p, c, 1, 2);
            Assert.That(c.InBattle, Is.True); Assert.That(c.TurnActionCommitted, Is.True);
            Assert.That(c.Enemies.Single(), Is.SameAs(enemy));
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(0, 0), map, c), Is.False);
            Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => new PlayerTurnEngine().EndTurn(p, c.DayPart, null, c));
            var battle = c.RampagingBattle.Machine;
            battle.ApplyRanged(c.Enemies, Array.Empty<AttackAllocation>(), c);
            battle.SetBlock(0, Element.Physical);
            var defense = battle.ResolveDefense(c.Enemies, Array.Empty<BlockAllocation>(), c);
            Assert.That(defense.TotalWounds, Is.EqualTo(2));
            Play(p, c, fury, true);
            var attack = battle.Attack(new[] { 0 }, c);
            Assert.That(attack.PrintedPower, Is.EqualTo(4)); Assert.That(attack.RequiredPower, Is.EqualTo(3));
            Assert.That(attack.Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(2));
            battle.FinishBattle(c);
            Assert.That(site.Enemy, Is.Null); Assert.That(p.Reputation, Is.EqualTo(1));
            Assert.That(c.RampagingBattle, Is.Null); Assert.That(c.InBattle, Is.False);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(0, 0), map, c), Is.False);
        }

        [Test]
        public void OrdinaryRunOneTurnRetainsNormalRoundCountdown()
        {
            var a = new PlayerState(); var b = new PlayerState();
            var game = new GameEngine(new[] { a, b }, 3);
            game.StartNewRound(new[] { new TacticCard("a", DayPart.Day, 1, 0), new TacticCard("b", DayPart.Day, 2, 0) });
            game.RunOneTurn();
            Assert.That(game.ClockTracker.CountdownStarted, Is.True);
            Assert.That(game.ClockTracker.RoundEnded, Is.False);
            game.RunOneTurn();
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
        }

        [Test]
        public void StrongUsesEngineForAThenAThenBReturnsOnlyPlayedCardsAndRestoresSourceAtRoundEnd()
        {
            var a = new PlayerState(); var b = new PlayerState();
            var map = Map(TerrainType.Plains, TerrainType.Plains, TerrainType.Plains);
            var game = new GameEngine(new[] { a, b }, 3, map: map, manaRandom: new Random(16));
            game.Clock.NextRound();
            var c = new ActionContext { DayPart = game.Clock.DayPart, MovementMap = map, ManaSource = game.Globals.Mana };
            var reserve = new[] { new DeedCard("basic_card_007", CardType.Action), new DeedCard("basic_card_014", CardType.Action) };
            a.Deck.SetDeck(reserve);
            var source = Add(a, "magic_016"); var march = Add(a, "basic_card_000");
            var promise = Add(a, "basic_card_008"); var discarded = Add(a, "basic_card_001");
            var removed = Add(a, "basic_card_016");
            game.StartNewRound(new[] { new TacticCard("order_a", DayPart.Night, 1, 0),
                new TacticCard("order_b", DayPart.Night, 2, 0) });
            // Five actual starting cards prevented automatic round-start refill from consuming the reserve.
            Exact(a.Deck.DrawPile, reserve);
            Assert.That(game.BeginNextTurn(c), Is.SameAs(a));
            a.Mana.AddToken(ManaColor.Blue); a.Mana.AddToken(ManaColor.Black);
            Play(a, c, march); Move(a, c, 1, 0);
            Play(a, c, source, true); Play(a, c, promise);
            Assert.That(a.Deck.TryDiscardExact(discarded), Is.True);
            Assert.That(a.Deck.TryRemovePermanentlyExact(removed), Is.True);
            Exact(a.Deck.PlayedCards, march, source, promise);
            a.Mana.AddToken(ManaColor.Red);
            c.SkipDraw = false; // Actual card rule must override a normal refill request.
            game.EndCurrentTurn(c);
            Exact(a.Deck.Hand, march, promise); Exact(a.Deck.DrawPile, reserve);
            Exact(a.Deck.RoundAsideCards, source); Exact(a.Deck.DiscardPile, discarded);
            Exact(a.Deck.RemovedCards, removed); Assert.That(a.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(a.Deck.PlayedCards, Is.Empty); Assert.That(c.ExtraTurn, Is.False);
            Assert.That(game.BeginNextTurn(c), Is.SameAs(a));
            Play(a, c, march); Move(a, c, 2, 0); Play(a, c, promise);
            c.SkipDraw = false; game.EndCurrentTurn(c);
            Exact(a.Deck.Hand, reserve); Assert.That(a.Deck.DrawPile, Is.Empty);
            Exact(a.Deck.RoundAsideCards, source);
            Assert.That(a.Deck.DiscardPile, Is.EquivalentTo(new[] { discarded, march, promise }));
            // B has neither hand nor deck: announce instead of taking a synthetic empty turn.
            Assert.That(game.CanAnnounceRoundEnd, Is.True);
            game.AnnounceRoundEnd();
            Assert.That(game.ClockTracker.RoundEnded, Is.False);
            Assert.That(game.BeginNextTurn(c), Is.SameAs(a)); game.EndCurrentTurn(c,new[]{a.Deck.Hand[0]});
            Assert.That(game.ClockTracker.RoundEnded, Is.True);
            Unique(a, source, march, promise, discarded, removed, reserve[0], reserve[1]);
            game.EndRound();
            Assert.That(a.Deck.RoundAsideCards, Is.Empty);
            Assert.That(a.Deck.DrawPile.Any(x => ReferenceEquals(x, source)), Is.True);
            Exact(a.Deck.RemovedCards, removed);
            Unique(a, source, march, promise, discarded, removed, reserve[0], reserve[1]);
        }
    }
}
