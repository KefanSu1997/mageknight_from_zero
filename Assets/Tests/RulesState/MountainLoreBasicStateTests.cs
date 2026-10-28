using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class MountainLoreBasicStateTests
    {
        private static readonly string[] DrawIds = { "basic_card_000", "basic_card_001",
            "basic_card_007", "basic_card_008", "basic_card_014", "basic_card_016",
            "basic_card_021", "basic_card_024" };
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard source, DeedCard[] draw)
            Setup(bool strong, TerrainType start, TerrainType end)
        {
            var p = new PlayerState();
            var c = new ActionContext { DayPart = DayPart.Day, CurrentTerrain = start, MovementMap = new MapState() };
            var terrains = new[] { start, end, TerrainType.Plains, TerrainType.Plains };
            for (int q = 0; q < terrains.Length; q++)
                c.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { terrains[q] });
            var originalBasics = CardJsonLoader.LoadBasicActions();
            var draw = DrawIds.Select(id =>
            {
                Assert.That(originalBasics.Any(card => card.Id == id), Is.True);
                return new DeedCard(id, CardType.Action);
            }).ToArray();
            p.Deck.SetDeck(draw);
            var source = new DeedCard("advanced_card_039", CardType.Action); p.Deck.Hand.Add(source);
            if (strong) p.Mana.AddToken(ManaColor.Green);
            Assert.That(p.HandLimit(), Is.EqualTo(5));
            Exact(p.Deck.DrawPile, draw);
            return (p, c, source, draw);
        }

        private static void Exact(IReadOnlyList<DeedCard> actual, params DeedCard[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(actual[i], Is.SameAs(expected[i]));
        }

        private static void OriginalNine(PlayerState p, DeedCard source, DeedCard[] draw)
        {
            var all = p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.DrawPile)
                .Concat(p.Deck.DiscardPile).Concat(p.Deck.RemovedCards).ToArray();
            Assert.That(all.Length, Is.EqualTo(9));
            foreach (var original in draw.Append(source))
                Assert.That(all.Count(c => ReferenceEquals(c, original)), Is.EqualTo(1), original.Id);
            Assert.That(p.Deck.RemovedCards, Is.Empty);
        }

        private static void PlayMove(PlayerState p, ActionContext c, DeedCard card, bool strong, int cost)
        {
            var original = CardJsonLoader.LoadAdvancedActions().Single(x => x.Id == card.Id);
            Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Green }));
            CardPlayActions.Play(p, c, card, original, strong);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(c.MountainLoreMode, Is.EqualTo(strong ? 2 : 1));
            Assert.That(c.MovementPool, Is.EqualTo(strong ? 5 : 3));
            Assert.That(c.NextDrawBonus, Is.Zero);
            var target = new AxialCoord(1, 0);
            Assert.That(MovementService.GetCost(target, c.MovementMap, c.DayPart, c), Is.EqualTo(cost));
            Assert.That(new MovementService().TryMoveUsingPool(p, target, c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(target));
            Assert.That(c.MovementPool, Is.EqualTo((strong ? 5 : 3) - cost));
            Assert.That(c.NextDrawBonus, Is.Zero);
        }

        private static void End(PlayerState p, ActionContext c, bool skip = false)
        {
            c.SkipDraw = skip;
            new PlayerTurnEngine().EndTurn(p, c.DayPart, null, c);
        }

        [TestCase(false, TerrainType.Plains, TerrainType.Hills, 3, 6)]
        [TestCase(true, TerrainType.Plains, TerrainType.Mountain, 5, 7)]
        [TestCase(true, TerrainType.Plains, TerrainType.Hills, 3, 6)]
        [TestCase(false, TerrainType.Hills, TerrainType.Plains, 2, 5)]
        public void ActualEndTerrainDeterminesOriginalOrderedRefill(bool strong, TerrainType start,
            TerrainType end, int cost, int expectedHand)
        {
            var (p, c, source, draw) = Setup(strong, start, end);
            PlayMove(p, c, source, strong, cost);
            if (end == TerrainType.Mountain)
            {
                Assert.That(c.MountainSafe, Is.True);
                Assert.That(CardTravelActions.IsSafeDestination(p, c, p.Position, c.OtherPlayers), Is.True);
            }
            // Deliberately stale display/context terrain: actual position/map must decide the reward.
            if (!strong && end == TerrainType.Hills) c.CurrentTerrain = start;
            End(p, c);
            Exact(p.Deck.Hand, draw.Take(expectedHand).ToArray());
            Exact(p.Deck.DrawPile, draw.Skip(expectedHand).ToArray());
            Exact(p.Deck.DiscardPile, source);
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.MountainLoreMode, Is.Zero);
            Assert.That(c.MovementPool, Is.Zero); Assert.That(c.MountainSafe, Is.False);
            Assert.That(c.LakeSafe, Is.False);
            Assert.That(c.TerrainCostOverride.ContainsKey(TerrainType.Mountain), Is.False);
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
            OriginalNine(p, source, draw);
        }

        [Test]
        public void SkippedRefillPreservesOneRewardThenLaterNormalRefillDoesNotRepeatIt()
        {
            var (p, c, source, draw) = Setup(false, TerrainType.Plains, TerrainType.Hills);
            PlayMove(p, c, source, false, 3); End(p, c, skip: true);
            Assert.That(c.MountainLoreMode, Is.Zero); Assert.That(c.NextDrawBonus, Is.EqualTo(1));
            Assert.That(p.Deck.Hand, Is.Empty); Exact(p.Deck.DrawPile, draw); Exact(p.Deck.DiscardPile, source);
            new PlayerTurnEngine().StartTurn(p, c.DayPart); End(p, c);
            Exact(p.Deck.Hand, draw.Take(6).ToArray()); Exact(p.Deck.DrawPile, draw.Skip(6).ToArray());
            Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.MountainLoreMode, Is.Zero);
            new PlayerTurnEngine().StartTurn(p, c.DayPart);
            // Spend two of those actual drawn cards, then a normal five-card refill can be observed.
            foreach (var card in new[] { draw[0], draw[2] })
                CardPlayActions.Play(p, c, card,
                    CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id));
            Assert.That(c.MovementPool, Is.EqualTo(4));
            for (int q = 2; q <= 3; q++)
                Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(q, 0), c.MovementMap, c), Is.True);
            Assert.That(c.MovementPool, Is.Zero);
            End(p, c);
            Exact(p.Deck.Hand, draw[1], draw[3], draw[4], draw[5], draw[6]);
            Exact(p.Deck.DrawPile, draw[7]);
            Assert.That(p.Deck.DiscardPile, Is.EquivalentTo(new[] { source, draw[0], draw[2] }));
            Assert.That(c.NextDrawBonus, Is.Zero); Assert.That(c.MountainLoreMode, Is.Zero);
            OriginalNine(p, source, draw);
        }

        [Test]
        public void HiddenPathMountainAndLakeSafetyExpireAtEndTurnAndDoNotReturnNextTurn()
        {
            var p = new PlayerState();
            var c = new ActionContext { DayPart = DayPart.Day, MovementMap = new MapState() };
            var mountain = new AxialCoord(1, 0); var lake = new AxialCoord(0, 1);
            c.MovementMap.Placed[p.Position] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            c.MovementMap.Placed[mountain] = new MapTile(TileSet.Countryside, 1, new[] { TerrainType.Mountain });
            c.MovementMap.Placed[lake] = new MapTile(TileSet.Countryside, 2, new[] { TerrainType.Lake });
            // Shared lifetime regression pays the printed blue fee before granting lake safety.
            p.Mana.AddToken(ManaColor.Blue);
            new HiddenPathEffect().Execute(p, c, option: 1);
            Assert.That(c.MovementPool, Is.EqualTo(1));
            Assert.That(CardTravelActions.IsSafeDestination(p, c, mountain), Is.True);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, lake), Is.True);
            Assert.That(MovementService.GetCost(mountain, c.MovementMap, c.DayPart, c), Is.EqualTo(5));
            Assert.That(MovementService.GetCost(lake, c.MovementMap, c.DayPart, c), Is.EqualTo(2));
            End(p, c, skip: true);
            Assert.That(c.MountainSafe, Is.False); Assert.That(c.LakeSafe, Is.False);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, mountain), Is.False);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, lake), Is.False);
            Assert.That(c.TerrainCostOverride.ContainsKey(TerrainType.Mountain), Is.False);
            Assert.That(c.TerrainCostOverride.ContainsKey(TerrainType.Lake), Is.False);
            new PlayerTurnEngine().StartTurn(p, c.DayPart);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, mountain), Is.False);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, lake), Is.False);
            Assert.That(c.MovementPool, Is.Zero); Assert.That(c.MountainLoreMode, Is.Zero);
        }

        [Test]
        public void MountainSafetyLostBeforeEndTurnRejectsBeforeCardsOrResourcesChange()
        {
            var (p, c, source, draw) = Setup(true, TerrainType.Plains, TerrainType.Mountain);
            PlayMove(p, c, source, true, 5);
            c.MountainSafe = false; // Explicitly exercise the agreed card-scoped end-turn safety gate.
            p.Mana.AddToken(ManaColor.Red);
            Assert.That(CardTravelActions.IsSafeDestination(p, c, p.Position, c.OtherPlayers), Is.False);
            Assert.Throws<InvalidOperationException>(() => End(p, c));
            Assert.That(c.MountainLoreMode, Is.EqualTo(2)); Assert.That(c.NextDrawBonus, Is.Zero);
            Assert.That(c.TerrainCostOverride[TerrainType.Mountain], Is.EqualTo(5));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
            Exact(p.Deck.PlayedCards, source); Exact(p.Deck.DrawPile, draw);
            Assert.That(p.Deck.DiscardPile, Is.Empty); Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(1, 0)));
            OriginalNine(p, source, draw);
        }
    }
}
