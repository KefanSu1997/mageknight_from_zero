using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 提供玩家在地圖上移動的基本驗證與消耗計算。
    /// </summary>
    public sealed class MovementService
    {
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
            var cost = TerrainCost.GetCost(terrain, dp); // 簡化：僅取第一邊的地形

            if (ctx != null)
            {
                if (ctx.TerrainCostOverride.TryGetValue(terrain, out int c))
                    cost = c;
                if (ctx.MoveForbidden.Contains(terrain))
                {
                    logger?.Log($"MoveFail P{p.Id} forbidden {terrain}");
                    return false;
                }
            }

            if (cost != int.MaxValue && crossWall && (ctx == null || ctx.TeleportRange == 0))
                cost += 1;

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
