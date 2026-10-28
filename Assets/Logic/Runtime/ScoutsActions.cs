using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using Monster = MK.Logic.Data.Monster;

namespace MK.Logic.Runtime
{
    public sealed record ScoutTarget(AxialCoord? Position = null, int TokenIndex = 0, EnemyTokenColor? SupplyColor = null);
    /// <summary>Private observation, not a public reveal or a draw from supply.</summary>
    public sealed record ScoutObservation(Monster Enemy, PlaceState Site, RuinTokenType? Ruin);

    public static class ScoutsActions
    {
        internal static ScoutObservation Preview(PlayerState player, ActionContext context, ScoutTarget target)
        {
            if (target == null || target.Position.HasValue == target.SupplyColor.HasValue || target.TokenIndex < 0)
                throw new InvalidOperationException("请选择一个地图上的盖放标记，或一种敌人供应的顶标记");
            if (target.SupplyColor.HasValue)
            {
                if (target.TokenIndex != 0 || context.EnemySupply == null || !context.EnemySupply.CanDraw(target.SupplyColor.Value))
                    throw new InvalidOperationException("所选颜色没有未使用的敌人顶标记");
                return new ScoutObservation(context.EnemySupply.Peek(target.SupplyColor.Value), null, null);
            }
            var map = context.MovementMap;
            var at = target.Position.Value;
            int q = at.Q - player.Position.Q, r = at.R - player.Position.R;
            int distance = (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2;
            if (map == null || !map.Placed.ContainsKey(at) || !map.Places.TryGetValue(at, out var site)
                || distance > 3 || map.RevealedSites.Contains(site)
                || site.Type == PlaceType.OrcRampaging || site.Type == PlaceType.Dragon)
                throw new InvalidOperationException("只能私看三格内实际地图上的一个正面朝下标记");
            if (site.Type == PlaceType.AncientRuin && site.RuinToken.HasValue && !site.RuinConquered)
            {
                if (target.TokenIndex != 0) throw new InvalidOperationException("请选择该地点唯一的遗迹标记");
                return new ScoutObservation(null, site, site.RuinToken.Value);
            }
            if (target.TokenIndex >= site.Defenders.Count || site.GuardNeedsReplacement)
                throw new InvalidOperationException("该地点没有所选的实际盖放敌人标记");
            return new ScoutObservation(site.Defenders[target.TokenIndex], site, null);
        }

        internal static void Observe(ActionContext context, ScoutObservation observation)
        {
            context.LastScoutObservation = observation;
            if (observation.Enemy != null) context.ScoutedEnemies.Add(observation.Enemy);
        }

        /// <summary>Only an actually defeated physical enemy pays the promised fame, once per mark.</summary>
        internal static int TakeDefeatBonus(ActionContext context, Monster enemy)
        {
            if (context == null) return 0;
            return context.ScoutedEnemies.RemoveAll(e => ReferenceEquals(e, enemy));
        }
    }
}
