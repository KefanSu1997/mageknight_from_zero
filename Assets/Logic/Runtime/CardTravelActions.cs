using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime
{
    /// <summary>原时光传送的可选落点操作：直接到达已揭示安全格，不花移动力、不经过中间格。</summary>
    public static class CardTravelActions
    {
        /// <summary>真实战斗结束后结算地道撤退；起点与地点城防豁免仅属于这次突袭。</summary>
        public static void FinishTunnelAssault(PlayerState player, ActionContext context, bool withdraw)
        {
            if (!context.TunnelAssaultActive || context.InBattle || context.CombatPhase != Phase.End)
                throw new InvalidOperationException("必须先完成地道突袭战斗");
            if (context.TunnelTargetSite != null)
            {
                var site = context.TunnelTargetSite;
                if (!context.MovementMap.Places.TryGetValue(context.TunnelDestination, out var current)
                    || !ReferenceEquals(current, site) || site.Defenders.Count != context.TunnelDefenders.Count
                    || site.Defenders.Where((guard, i) => !ReferenceEquals(guard, context.TunnelDefenders[i])).Any())
                    throw new InvalidOperationException("突袭地点或守军发生变化，不能结算其他目标");
                bool conquered = context.TunnelDefenders.Count > 0 && context.TunnelDefenders.All(context.IsTunnelDefenderDefeated);
                if (!conquered && !withdraw)
                    throw new InvalidOperationException("仍有守军时必须撤回地道起点");
                if (conquered && withdraw)
                    throw new InvalidOperationException("已征服地点，必须留在该地点");
                if (context.EnemySupply != null && context.TunnelDefenders
                    .Where(m => context.IsTunnelDefenderDefeated(m) || site.Type != PlaceType.City)
                    .Any(m => !context.EnemySupply.IsInUse(m)))
                    throw new InvalidOperationException("突袭守军不属于实际供给，不能弃置");
            }
            if (!withdraw && context.TunnelTargetSite == null && context.TunnelTargetPlayer == null && context.Enemies != null
                && Enumerable.Range(0,context.Enemies.Count).Any(i => !context.DefeatedEnemyIndices.Contains(i)))
                throw new InvalidOperationException("仍有守军时必须撤回地道起点");
            if (context.TunnelTargetSite != null)
            {
                var site = context.TunnelTargetSite;
                var defeated = context.TunnelDefenders.Where(context.IsTunnelDefenderDefeated).ToArray();
                var survivors = context.TunnelDefenders.Where(m => !context.IsTunnelDefenderDefeated(m)).ToArray();
                if (context.EnemySupply != null)
                    foreach (var guard in context.TunnelDefenders.Where(m => context.IsTunnelDefenderDefeated(m) || site.Type != PlaceType.City))
                        context.EnemySupply.Discard(guard);
                if (site.Type == PlaceType.City)
                {
                    foreach (var guard in defeated) site.CityShields.Add(player.Id);
                    site.Enemy = survivors.FirstOrDefault();
                    site.AdditionalGuards.Clear();
                    site.AdditionalGuards.AddRange(survivors.Skip(1));
                    if (survivors.Length == 0)
                    {
                        site.GuardNeedsReplacement = false;
                        site.OwnerId = site.CityShields.GroupBy(id => id).OrderByDescending(g => g.Count()).First().Key;
                    }
                }
                else if (survivors.Length == 0)
                {
                    site.Enemy = null;
                    site.AdditionalGuards.Clear();
                    site.GuardNeedsReplacement = false;
                    site.OwnerId = player.Id;
                    if (site.Type == PlaceType.MageTower) context.PendingSpellReward = true;
                }
                else if (site.Type != PlaceType.City)
                {
                    site.Enemy = null;
                    site.AdditionalGuards.Clear();
                    site.GuardNeedsReplacement = true;
                }
            }
            if (withdraw) player.Position = context.TunnelOrigin;
            context.CurrentTerrain = context.MovementMap.Placed[player.Position].Edges[0];
            context.IgnoreSiteFortifications = context.TunnelPreviousSiteIgnore;
            context.TunnelAssaultActive = context.ReturnAfterRetreat = false;
            context.TunnelTargetPlayer = null;
            context.FortifiedSite = false;
        }

        public static bool TryTemporalPortal(PlayerState player, ActionContext context, AxialCoord target,
            IEnumerable<PlayerState> otherPlayers = null)
        {
            var map = context.MovementMap;
            int dq = target.Q - player.Position.Q, dr = target.R - player.Position.R;
            int distance = (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
            if (!context.TemporalPortalPending || distance < 1 || distance > context.TeleportRange
                || map == null || !map.Placed.TryGetValue(target, out var tile) || tile.Edges.Length == 0)
                return false;
            if (!IsSafeDestination(player, context, target, otherPlayers)) return false;
            player.Position = target;
            context.CurrentTerrain = tile.Edges[0];
            EndTemporalPortal(context);
            return true;
        }

        public static bool IsSafeDestination(PlayerState player, ActionContext context, AxialCoord target,
            IEnumerable<PlayerState> otherPlayers = null)
        {
            var map = context.MovementMap;
            if (map == null || !map.Placed.TryGetValue(target, out var tile) || tile.Edges.Length == 0) return false;
            var terrain = tile.Edges[0];
            map.Places.TryGetValue(target, out var place);
            bool fortified = place?.Type == PlaceType.Keep || place?.Type == PlaceType.City || place?.Type == PlaceType.MageTower;
            bool rampaging = place?.Type == PlaceType.OrcRampaging || place?.Type == PlaceType.Dragon;
            bool sharedCity = place?.Type == PlaceType.City && place.Conquered;
            if ((terrain == TerrainType.Mountain && !context.MountainSafe)
                || (terrain == TerrainType.Lake && !context.LakeSafe)
                || fortified && !place.Conquered || rampaging && place.Enemy != null
                || place?.Type == PlaceType.Keep && place.OwnerId.HasValue && place.OwnerId != player.Id
                || !sharedCity && otherPlayers?.Any(p => !ReferenceEquals(p, player) && p.Position.Equals(target)) == true)
                return false;
            return true;
        }

        public static void EndTemporalPortal(ActionContext context)
        {
            if (!context.TemporalPortalPending) return;
            context.TemporalPortalPending = false;
            context.TeleportRange = 0;
            context.TeleportMustEndSafe = false;
            context.IgnoreRampaging = false;
        }
    }
}
