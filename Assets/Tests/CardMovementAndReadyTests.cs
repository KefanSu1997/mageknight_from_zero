using System;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CardMovementAndReadyTests
    {
        private static ActionContext Map(TerrainType terrain, DayPart time = DayPart.Day)
        {
            var ctx = new ActionContext { MovementMap = new MapState(), DayPart = time,
                TargetHex = new AxialCoord(1, 0), TargetTerrain = terrain };
            foreach (int q in new[] { 0, 1, 2 })
                ctx.MovementMap.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { terrain });
            return ctx;
        }

        [TestCase(false, 3)] [TestCase(true, 2)]
        public void DruidDiscountKeepsHexAndTerrainScopesSeparate(bool strong, int otherCost)
        {
            var ctx = Map(TerrainType.Forest); var player = new PlayerState();
            new DruidicPathsEffect(strong ? 4 : 2, strong).Execute(player, ctx);
            Assert.That(MovementService.GetCost(new AxialCoord(1, 0), ctx.MovementMap, ctx.DayPart, ctx), Is.EqualTo(2));
            Assert.That(MovementService.GetCost(new AxialCoord(2, 0), ctx.MovementMap, ctx.DayPart, ctx), Is.EqualTo(otherCost));
            Assert.That(ctx.TerrainCostOverride, Is.Empty);
            Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(1, 0), ctx.MovementMap, ctx), Is.True);
            Assert.That(ctx.MovementPool, Is.EqualTo(strong ? 2 : 0));
        }

        [TestCase(TerrainType.Forest, DayPart.Night, 4)]
        [TestCase(TerrainType.Desert, DayPart.Day, 4)]
        [TestCase(TerrainType.Desert, DayPart.Night, 2)]
        [TestCase(TerrainType.Plains, DayPart.Day, 2)]
        [TestCase(TerrainType.Mountain, DayPart.Day, int.MaxValue)]
        public void DruidDiscountRespectsTimeFloorAndImpassableTerrain(TerrainType terrain, DayPart time, int expected)
        {
            var ctx = Map(terrain, time);
            new DruidicPathsEffect(4, true).Execute(new PlayerState(), ctx);
            Assert.That(MovementService.GetCost(new AxialCoord(1, 0), ctx.MovementMap, time, ctx), Is.EqualTo(expected));
        }

        [Test]
        public void RejectedMoveDoesNotSpendPoolOrChangePosition()
        {
            var ctx = Map(TerrainType.Forest); var player = new PlayerState(); ctx.MovementPool = 9;
            Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(2, 0), ctx.MovementMap, ctx), Is.False);
            Assert.That(player.Position.Q, Is.Zero); Assert.That(ctx.MovementPool, Is.EqualTo(9));
        }

        [TestCase(1, true, 4)] [TestCase(2, true, 2)] [TestCase(3, false, 6)] [TestCase(4, false, 6)]
        public void IntimidateReadiesOnlyOwnedLevelOneOrTwoUnits(int level, bool success, int remaining)
        {
            var player = new PlayerState(); var ctx = new ActionContext(); var unit = Unit(level); player.Units.Add(unit);
            new IntimidateEffect(true).Execute(player, ctx);
            Assert.That(CardUnitActions.TryReady(player, ctx, unit), Is.EqualTo(success));
            Assert.That(ctx.InfluencePool, Is.EqualTo(remaining)); Assert.That(unit.IsReady, Is.EqualTo(success));
        }

        [Test]
        public void FailedSecondReadyKeepsRemainingInfluence()
        {
            var player = new PlayerState(); var ctx = new ActionContext(); var first = Unit(2); var second = Unit(2);
            player.Units.Add(first); player.Units.Add(second); new IntimidateEffect(true).Execute(player, ctx);
            Assert.That(CardUnitActions.TryReady(player, ctx, first), Is.True);
            Assert.That(CardUnitActions.TryReady(player, ctx, second), Is.False);
            Assert.That(ctx.InfluencePool, Is.EqualTo(2)); Assert.That(second.IsReady, Is.False);
        }

        [Test]
        public void ReadyAndHealingDoNotConfuseCommandTokenWithWounds()
        {
            var unit = Unit(2); unit.AddWounds(2); unit.Ready();
            Assert.That(unit.IsReady, Is.True); Assert.That(unit.CanActivate, Is.False);
            Assert.That(unit.Wounds, Is.EqualTo(2));
            unit.Heal(2); Assert.That(unit.CanActivate, Is.True);
            unit.Exhaust(); unit.AddWounds(1); unit.Heal(1);
            Assert.That(unit.IsReady, Is.False);
            unit.Destroy(); unit.Ready(); Assert.That(unit.CanActivate, Is.False);
        }

        [Test]
        public void EndTurnExpiresEffectsEvenWhenDrawIsSkippedAndNextTurnDoesNotReadyUnits()
        {
            var player = new PlayerState(); var unit = Unit(2); player.Units.Add(unit);
            var ctx = Map(TerrainType.Forest); new DruidicPathsEffect(2, false).Execute(player, ctx);
            new IntimidateEffect(true).Execute(player, ctx); ctx.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, ctx);
            Assert.That(ctx.HexMoveReduction, Is.Empty); Assert.That(ctx.ReadyInfluencePerLevel, Is.Zero);
            Assert.That(ctx.MovementPool + ctx.InfluencePool, Is.Zero);
            new PlayerTurnEngine().StartTurn(player, DayPart.Day);
            Assert.That(unit.IsReady, Is.False);
            unit.NewRound(); Assert.That(unit.IsReady, Is.True);
        }

        private static UnitState Unit(int level)
        {
            var unit = new UnitState(new UnitCard("test", "Fixture", "Fixture", level, 4, 6,
                RecruitLocation.Village, Array.Empty<AttackProfile>(), Array.Empty<Ability>()));
            unit.Exhaust(); return unit;
        }
    }
}
