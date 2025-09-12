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
                if (terrain == TerrainType.Lake && ctx.RequireBlueForLake)
                {
                    var need = new Data.ManaCost(new()
                    {
                        { Element.Ice, 1 }
                    });
                    if (!p.Mana.Pay(need, p))
                    {
                        logger?.Log($"MoveFail P{p.Id} lakeCost");
                        return false;
                    }
                }
            }

            if (crossWall && (ctx == null || ctx.TeleportRange == 0))
                cost += 1;

            if (movePoints < cost)
            {
                logger?.Log($"MoveFail P{p.Id} need {cost}");
                return false;
            }
            logger?.Log($"Move P{p.Id} {p.Position} -> {to} cost {cost}");
            p.Position = to;
            return true;
        }
    }
}
