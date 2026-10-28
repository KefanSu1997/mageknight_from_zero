using MK.Logic.Core;
using MK.Logic.Data;
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
            // Printed rulebook p7: entering a city costs two regardless of its terrain.
            if (map.Places.TryGetValue(to, out var site) && site.Type == PlaceType.City) terrain = TerrainType.Plains;
            if (ctx?.MoveForbidden.Contains(terrain) == true) return int.MaxValue;
            int cost = ctx != null && ctx.TerrainCostOverride.TryGetValue(terrain, out int replacement)
                ? replacement : TerrainCost.GetCost(terrain, dp);
            if (cost == int.MaxValue) return cost;
            int reduction = ctx == null ? 0 : ctx.HexMoveReduction.GetValueOrDefault(to) + ctx.TerrainMoveReduction.GetValueOrDefault(terrain);
            // A discount cannot make an already cheaper hex more expensive, or open impassable terrain.
            if (reduction > 0) cost = System.Math.Min(cost, System.Math.Max(2, cost - reduction));
            int oneFloorReduction = ctx?.TerrainMoveReductionToOne.GetValueOrDefault(terrain) ?? 0;
            if (oneFloorReduction > 0) cost = System.Math.Min(cost, System.Math.Max(1, cost - oneFloorReduction));
            int zeroFloorReduction = ctx?.TerrainMoveReductionToZero.GetValueOrDefault(terrain) ?? 0;
            if (zeroFloorReduction > 0) cost = System.Math.Max(0, cost - zeroFloorReduction);
            if (crossWall && (ctx == null || ctx.TeleportRange == 0)) cost++;
            return cost;
        }

        /// <summary>One flight move, with its own distance fee and safe landing; it grants no later immunity.</summary>
        internal bool TryFlightUsingPool(PlayerState player, AxialCoord target, ActionContext context,
            IEnumerable<PlayerState> otherPlayers = null)
        {
            if (context.PendingRepeatedEffect != null || context.InBattle || context.TurnActionCommitted || context.RestedThisTurn)
                return false;
            int dq = target.Q - player.Position.Q, dr = target.R - player.Position.R;
            int distance = (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(dq + dr)) / 2;
            if (distance < 1 || distance > 2 || context.MovementPool < (distance == 2 ? 2 : 0)
                || !CardTravelActions.IsSafeDestination(player, context, target, otherPlayers ?? context.OtherPlayers)) return false;
            context.MovementPool -= distance == 2 ? 2 : 0;
            player.Position = target;
            context.CurrentTerrain = context.MovementMap.Placed[target].Edges[0];
            context.MovedThisTurn = true;
            SiteVisibility.RevealVisible(player, context.MovementMap, context.DayPart, context);
            // Flight itself never provokes. Normal later moves still use RampagingEncounter.AfterMove.
            return true;
        }

        /// <summary>Consume the shared card-generated pool only after the real move succeeds.</summary>
        public bool TryMoveUsingPool(PlayerState player, AxialCoord to, MapState map, ActionContext ctx,
            bool crossWall = false, DeedCard harvestCard = null, ManaColor? harvestColor = null)
        {
            // 收获是这次单格移动的可选组成部分；先验证材料，不能移动后再回填旧时机。
            var crystal = harvestCard == null ? (ManaColor?)null
                : CardHarvestActions.Validate(player, ctx, harvestCard, harvestColor);
            int cost = GetCost(to, map, ctx.DayPart, ctx, crossWall);
            if (!TryMove(player, to, map, ctx.MovementPool, ctx.DayPart, ctx, crossWall)) return false;
            ctx.MovementPool -= cost;
            ctx.CurrentTerrain = map.Placed[to].Edges[0];
            if (crystal.HasValue) CardHarvestActions.Resolve(player, ctx, harvestCard, crystal.Value);
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
            => TryMoveCore(p,to,map,movePoints,dp,ctx,crossWall,logger,false);

        internal bool TryAssaultMoveUsingPool(PlayerState player, AxialCoord target, MapState map, ActionContext context)
        {
            int cost=GetCost(target,map,context.DayPart,context);
            if(!TryMoveCore(player,target,map,context.MovementPool,context.DayPart,context,false,null,true))return false;
            context.MovementPool-=cost;
            context.CurrentTerrain=map.Placed[target].Edges[0];
            return true;
        }

        private bool TryMoveCore(PlayerState p, AxialCoord to, MapState map, int movePoints,
            DayPart dp, ActionContext? ctx, bool crossWall, IGameLogger? logger, bool assault)
        {
            if (ctx?.PendingRepeatedEffect != null || ctx?.TurnActionCommitted == true || ctx?.InBattle == true) return false;
            int dq = to.Q - p.Position.Q;
            int dr = to.R - p.Position.R;
            int distance = (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(dq + dr)) / 2;
            if ((distance != 1 && !(distance == 2 && ctx?.SpaceBendingActive == true)) || movePoints < 0)
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

            if (map.Places.TryGetValue(to, out var occupied)
                && (occupied.Type == PlaceType.OrcRampaging || occupied.Type == PlaceType.Dragon)
                && occupied.Enemy != null) return false;

            // Ordinary movement must not silently occupy a hostile fortified site. Entry
            // needs an assault transaction that binds its actual defenders and retreat
            // origin. Special travel/assault effects have their own explicit entry path.
            if (!assault && occupied != null
                && (occupied.Type == PlaceType.Keep || occupied.Type == PlaceType.MageTower || occupied.Type == PlaceType.City)
                && (!occupied.Conquered || occupied.Type == PlaceType.Keep && occupied.OwnerId != p.Id))
                return false;

            if (cost == int.MaxValue || movePoints < cost)
            {
                logger?.Log($"MoveFail P{p.Id} need {cost}");
                return false;
            }
            if (terrain == TerrainType.Lake && ctx != null && ctx.RequireBlueForLake
                && !p.Mana.TryPayExactColors(new[] { ManaColor.Blue }, ctx, p))
            {
                logger?.Log($"MoveFail P{p.Id} lakeCost");
                return false;
            }
            logger?.Log($"Move P{p.Id} {p.Position} -> {to} cost {cost}");
            var from = p.Position;
            p.Position = to;
            if (ctx != null) ctx.MovedThisTurn = true;
            Map.SiteVisibility.RevealVisible(p, map, dp, ctx);
            if (ctx != null) RampagingEncounter.AfterMove(p, map, from, to, ctx, assault);
            return true;
        }
    }
}
