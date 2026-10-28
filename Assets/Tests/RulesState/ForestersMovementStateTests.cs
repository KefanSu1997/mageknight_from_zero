using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ForestersMovementStateTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);

        private (PlayerState, UnitState, ActionContext) Route(TerrainType terrain, DayPart time)
        {
            var player = new PlayerState();
            var unit = new UnitState(UnitJsonLoader.Load(Folder, "army_2_000"));
            player.Units.Add(unit);
            var context = new ActionContext { MovementMap = new MapState(), DayPart = time, SkipDraw = true };
            context.MovementMap.Placed[new AxialCoord(0, 0)] = new MapTile(TileSet.Countryside, 0, new[] { TerrainType.Plains });
            context.MovementMap.Placed[new AxialCoord(1, 0)] = new MapTile(TileSet.Countryside, 1, new[] { terrain });
            return (player, unit, context);
        }

        private static DeedCard PlayOriginal(PlayerState p, ActionContext c, string id)
        {
            var original = (id.StartsWith("basic_card_") ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(x => x.Id == id);
            var card = new DeedCard(id, CardType.Action);
            p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, original, false);
            return card;
        }

        [TestCase(TerrainType.Forest, DayPart.Day, 2)]
        [TestCase(TerrainType.Forest, DayPart.Night, 4)]
        [TestCase(TerrainType.Hills, DayPart.Day, 2)]
        [TestCase(TerrainType.Swamp, DayPart.Day, 4)]
        [TestCase(TerrainType.Plains, DayPart.Day, 2)]
        public void OriginalMovementDiscountIsSpentOnRealDestination(TerrainType terrain, DayPart time, int cost)
        {
            var (p, unit, c) = Route(terrain, time);
            Assert.That(unit.Card.Level, Is.EqualTo(1));
            Assert.That(unit.Card.Armor, Is.EqualTo(4));
            Assert.That(unit.Card.InfluenceCost, Is.EqualTo(5));
            Assert.That(unit.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village));
            Assert.That(unit.Card.Actions[0].Cost, Is.Empty);
            CardUnitActions.Use(p, c, unit, 0);
            Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.That(unit.IsReady, Is.False);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(p, c, unit, 0));
            Assert.That(c.MovementPool, Is.EqualTo(2));
            var march = PlayOriginal(p, c, "basic_card_000");
            Assert.That(c.MovementPool, Is.EqualTo(4));
            var destination = new AxialCoord(1, 0);
            Assert.That(MovementService.GetCost(destination, c.MovementMap, time, c), Is.EqualTo(cost));
            Assert.That(new MovementService().TryMoveUsingPool(p, destination, c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(destination));
            Assert.That(c.MovementPool, Is.EqualTo(4 - cost));
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(march));
            Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FrostBridgeReachesZeroInEitherOrderAndDiscountExpiresAtRealTurnEnd(bool unitFirst)
        {
            var (p, unit, c) = Route(TerrainType.Swamp, DayPart.Day);
            var turns = new PlayerTurnEngine();
            turns.StartTurn(p, DayPart.Day);
            if (unitFirst) CardUnitActions.Use(p, c, unit, 0);
            var bridge = PlayOriginal(p, c, "advanced_card_009");
            if (!unitFirst) CardUnitActions.Use(p, c, unit, 0);
            var destination = new AxialCoord(1, 0);
            Assert.That(MovementService.GetCost(destination, c.MovementMap, c.DayPart, c), Is.Zero);
            Assert.That(new MovementService().TryMoveUsingPool(p, destination, c.MovementMap, c), Is.True);
            Assert.That(p.Position, Is.EqualTo(destination));
            Assert.That(c.MovementPool, Is.EqualTo(4));
            turns.EndTurn(p, DayPart.Day, null, c);
            turns.StartTurn(p, DayPart.Day);
            Assert.That(c.TerrainMoveReductionToZero, Is.Empty);
            Assert.That(c.MovementPool, Is.Zero);
            Assert.That(MovementService.GetCost(destination, c.MovementMap, c.DayPart, c), Is.EqualTo(5));
            Assert.That(unit.IsReady, Is.False);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(bridge));
        }

        [TestCase(TerrainType.Lake)]
        [TestCase(TerrainType.Mountain)]
        public void DiscountDoesNotOpenImpassableTerrain(TerrainType terrain)
        {
            var (p, unit, c) = Route(terrain, DayPart.Day);
            CardUnitActions.Use(p, c, unit, 0);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.False);
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(c.MovementPool, Is.EqualTo(2));
        }

        [Test]
        public void AlternativePrintedBlockUsesExistingBattleConsumer()
        {
            var (p, unit, _) = Route(TerrainType.Plains, DayPart.Day);
            var sideways = new DeedCard("basic_card_000", CardType.Action);
            p.Deck.Hand.Add(sideways);
            var battle = new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(x => x.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            battle.Advance();
            battle.UseUnit(unit, 1);
            Assert.That(battle.Context.BlockPool, Is.EqualTo(3));
            Assert.That(battle.Context.TerrainMoveReductionToZero, Is.Empty);
            battle.PlaySideways(sideways);
            Assert.That(battle.BlockPreview(0).Effective, Is.EqualTo(4));
            battle.Block(0);
            battle.Advance(); battle.Advance();
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(unit.IsReady, Is.False);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(sideways));
        }
    }
}
