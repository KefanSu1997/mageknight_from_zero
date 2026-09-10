using MK.Logic.Core;
using MK.Logic.Runtime.Map;
using System.Collections.Generic;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 提供玩家在地圖上移動的基本驗證與消耗計算。
    /// </summary>
    public sealed class MovementService
    {
        public static int GetCost(AxialCoord to, MapState map, DayPart dp, ActionContext ctx = null, bool crossWall = false)
        {
            if (!map.Placed.TryGetValue(to, out var tile) || tile.Edges.Length == 0) return int.MaxValue;
            var terrain = tile.Edges[0];
            if (ctx?.MoveForbidden.Contains(terrain) == true) return int.MaxValue;
            int cost = ctx != null && ctx.TerrainCostOverride.TryGetValue(terrain, out int replacement)
                ? replacement : TerrainCost.GetCost(terrain, dp);
            if (cost == int.MaxValue) return cost;
            int reduction = ctx == null ? 0 : ctx.HexMoveReduction.GetValueOrDefault(to) + ctx.TerrainMoveReduction.GetValueOrDefault(terrain);
            // A discount cannot make an already cheaper hex more expensive, or open impassable terrain.
            if (reduction > 0) cost = System.Math.Min(cost, System.Math.Max(2, cost - reduction));
            if (crossWall && (ctx == null || ctx.TeleportRange == 0)) cost++;
            return cost;
        }

        /// <summary>Consume the shared card-generated pool only after the real move succeeds.</summary>
        public bool TryMoveUsingPool(PlayerState player, AxialCoord to, MapState map, ActionContext ctx, bool crossWall = false)
        {
            int cost = GetCost(to, map, ctx.DayPart, ctx, crossWall);
            if (!TryMove(player, to, map, ctx.MovementPool, ctx.DayPart, ctx, crossWall)) return false;
            ctx.MovementPool -= cost;
            ctx.CurrentTerrain = map.Placed[to].Edges[0];
            return true;
        }
        /// <summary>
        /// 嘗試從玩家當前位置移動至指定座標，
        /// 消耗等於地形表中的移動值。
        /// 若移動點不存在於地圖中則返回 false。
        /// </summary>
        public bool TryMove(
            PlayerState p,
            AxialCoord to,
            MapState map,
            int movePoints,
            DayPart dp,
            ActionContext? ctx = null,
            bool crossWall = false,
            IGameLogger? logger = null)
        {
            int dq = to.Q - p.Position.Q;
            int dr = to.R - p.Position.R;
            int distance = (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(dq + dr)) / 2;
            if (distance != 1 || movePoints < 0)
            {
                logger?.Log($"MoveFail P{p.Id} notAdjacent");
                return false;
            }
            if (!map.Placed.TryGetValue(to, out var tile))
            {
                logger?.Log($"MoveFail P{p.Id} invalid {to}");
                return false;
            }
            var terrain = tile.Edges[0];
            var cost = GetCost(to, map, dp, ctx, crossWall);

            if (cost == int.MaxValue || movePoints < cost)
            {
                logger?.Log($"MoveFail P{p.Id} need {cost}");
                return false;
            }
            if (terrain == TerrainType.Lake && ctx != null && ctx.RequireBlueForLake
                && !p.Mana.Pay(new Data.ManaCost(new() { { Element.Ice, 1 } }), p))
            {
                logger?.Log($"MoveFail P{p.Id} lakeCost");
                return false;
            }
            logger?.Log($"Move P{p.Id} {p.Position} -> {to} cost {cost}");
            p.Position = to;
            return true;
        }
    }
}
