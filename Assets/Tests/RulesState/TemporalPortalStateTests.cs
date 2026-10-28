using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class TemporalPortalStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static void Play(PlayerState p, ActionContext c, bool strong = false, int option = 0)
            => new ActionSystem().Play(CardJsonLoader.LoadAdvancedActions().Single(a => a.Id == "advanced_card_032"), p, c, strong, option);
        private static ActionContext Map(TerrainType terrain = TerrainType.Forest)
        {
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState() };
            for (int q = 0; q < 4; q++) c.MovementMap.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { terrain });
            return c;
        }
        private static PlayerState Player(int cards = 15)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Blue, 2);
            for (int i = 0; i < cards; i++) p.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
            return p;
        }

        [TestCase(false, 0, 1, 1)] [TestCase(true, 0, 2, 1)] [TestCase(true, 1, 1, 2)]
        public void OriginalChoicesMoveOnceThenActuallyRefill(bool strong, int option, int distance, int bonus)
        {
            var p = Player(); var c = Map(); Play(p, c, strong, option);
            Assert.That(c.NextDrawBonus, Is.EqualTo(bonus)); Assert.That(c.TeleportRange, Is.EqualTo(distance));
            Assert.That(p.Mana.Tokens[ ManaColor.Blue ], Is.EqualTo(strong ? 1 : 2));
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(distance, 0)), Is.True);
            Assert.That(p.Position.Q, Is.EqualTo(distance)); Assert.That(c.MovementPool, Is.Zero);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(0, 0)), Is.False);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(5 + bonus)); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(10 - bonus));
            Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.TemporalPortalPending, Is.False);
        }

        [TestCase(false, 0, 6)] [TestCase(true, 1, 7)]
        public void OptionalMoveCanBeSkippedAndStillDraws(bool strong, int option, int hand)
        {
            var p = Player(); var c = Map(); Play(p, c, strong, option);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Position.Q, Is.Zero); Assert.That(p.Deck.Hand.Count, Is.EqualTo(hand));
            Assert.That(c.TeleportRange, Is.Zero); Assert.That(c.IgnoreRampaging, Is.False);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0)), Is.False);
        }

        [TestCase(0)] [TestCase(2)] [TestCase(4)]
        public void InvalidDistanceOrUnrevealedSpacePreservesPermission(int q)
        {
            var p = Player(); var c = Map(); Play(p, c);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(q, 0)), Is.False);
            Assert.That(p.Position.Q, Is.Zero); Assert.That(c.TemporalPortalPending, Is.True); Assert.That(c.NextDrawBonus, Is.EqualTo(1));
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0)), Is.True);
        }

        [TestCase(TerrainType.Lake)] [TestCase(TerrainType.Mountain)]
        public void ImpassableTerrainNeedsAnActualSafetyEffect(TerrainType terrain)
        {
            var p = Player(); var c = Map(terrain); Play(p, c);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0)), Is.False);
            if (terrain == TerrainType.Lake) c.LakeSafe = true; else c.MountainSafe = true;
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0)), Is.True);
        }

        [TestCase(PlaceType.Keep, false)] [TestCase(PlaceType.City, false)]
        [TestCase(PlaceType.OrcRampaging, false)] [TestCase(PlaceType.Dragon, false)]
        [TestCase(PlaceType.Dungeon, true)] [TestCase(PlaceType.AncientRuin, true)]
        public void SafetyDistinguishesForcedCombatFromOptionalAdventureSites(PlaceType type, bool expected)
        {
            var p = Player(); var c = Map(); Play(p, c); var to = new AxialCoord(1, 0);
            c.MovementMap.Places[to] = new PlaceState(type) { Enemy = new Monster("fixture", 4, 4, Element.Physical, 2, Array.Empty<Ability>()) };
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, to), Is.EqualTo(expected));
            Assert.That(p.Wounds, Is.Zero);
        }

        [Test]
        public void EnemyKeepAndOccupiedSpaceAreUnsafeButConqueredCityCanBeShared()
        {
            var p = Player(); var other = new PlayerState(2, "Other") { Position = new AxialCoord(1, 0) }; var c = Map(); Play(p, c);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, other.Position, new[] { other }), Is.False);
            c.MovementMap.Places[other.Position] = new PlaceState(PlaceType.Keep) { OwnerId = 2 };
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, other.Position), Is.False);
            c.MovementMap.Places[other.Position] = new PlaceState(PlaceType.City);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, other.Position, new[] { other }), Is.True);
        }

        [Test]
        public void TwoHexPortalDoesNotEnterOrFightTheIntermediateHex()
        {
            var p = Player(); var c = Map(); Play(p, c, true);
            c.MovementMap.Places[new AxialCoord(1, 0)] = new PlaceState(PlaceType.OrcRampaging)
                { Enemy = new Monster("fixture", 4, 4, Element.Physical, 2, Array.Empty<Ability>()) };
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(2, 0)), Is.True);
            Assert.That(p.Wounds, Is.Zero); Assert.That(p.Fame, Is.Zero);
            Assert.That(c.MovementMap.Places[new AxialCoord(1, 0)].Enemy, Is.Not.Null);
        }

        [TestCase(false, 1)] [TestCase(true, -1)] [TestCase(true, 2)]
        public void InvalidChoiceNeverPaysMana(bool strong, int option)
        {
            var p = Player(); var c = Map(); Assert.Throws<InvalidOperationException>(() => Play(p, c, strong, option));
            Assert.That(p.Mana.Tokens[ ManaColor.Blue ], Is.EqualTo(2)); Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.TemporalPortalPending, Is.False);
        }

        [Test]
        public void MissingBlueCannotGrantPortalOrBonus()
        {
            var p = Player(); var c = Map(); p.Mana.ResetTokens();
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true));
            Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.TemporalPortalPending, Is.False);
        }

        [Test]
        public void SkippedRefillKeepsBonusUntilNextRefillOnly()
        {
            var p = Player(); var c = Map(); Play(p, c, true, 1); c.SkipDraw = true;
            var turn = new PlayerTurnEngine(); turn.EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.NextDrawBonus, Is.EqualTo(2)); Assert.That(c.TemporalPortalPending, Is.False);
            turn.EndTurn(p, DayPart.Night, null, c); Assert.That(p.Deck.Hand.Count, Is.EqualTo(7));
            foreach (var card in p.Deck.Hand.ToArray()) p.Deck.TryDiscardExact(card);
            turn.EndTurn(p, DayPart.Day, null, c); Assert.That(p.Deck.Hand.Count, Is.EqualTo(5)); Assert.That(c.NextDrawBonus, Is.Zero);
        }

        [TestCase(DayPart.Day, -7)] [TestCase(DayPart.Night, 7)]
        public void RefillUsesHandLimitRatherThanReputationOrNight(DayPart day, int reputation)
        {
            var p = Player(); p.Reputation = reputation; p.KeepOrCastlePenalty = true; var c = Map(); Play(p, c);
            new PlayerTurnEngine().EndTurn(p, day, null, c);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(6));
        }

        [Test]
        public void EmptyDeckNeverFabricatesCardsOrReshufflesDiscard()
        {
            var p = Player(2); p.Deck.GainToDiscard(new DeedCard("basic_card_021", CardType.Action)); var c = Map(); Play(p, c, true, 1);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(2)); Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(1)); Assert.That(c.NextDrawBonus, Is.Zero);
        }

        [TestCase(8, 5)] [TestCase(24, 6)] [TestCase(48, 6)] [TestCase(80, 7)]
        public void NewRoundAndTurnRefillUseTheSameLevelHandLimit(int fame, int limit)
        {
            var p = Player(); p.Fame = fame; p.Reputation = -7;
            new GameEngine(new[] { p }, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(limit));
            foreach (var card in p.Deck.Hand.ToArray()) p.Deck.TryDiscardExact(card);
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, null, Map());
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(limit));
        }
    }
}
