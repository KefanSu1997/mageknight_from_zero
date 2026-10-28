using MK.Logic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 地道穿行 / 地道攻擊：提供特殊傳送能力。
    /// </summary>
    public sealed class TunnelTravelEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public TunnelTravelEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 || ctx.TravelPath.Count > 3)
                throw new InvalidOperationException("地道穿行必须选择至多三格路径");
            if (ctx.InBattle || ctx.InNegotiation || ctx.TurnActionCommitted || ctx.TunnelAssaultActive)
                throw new InvalidOperationException("地道穿行只能在移动阶段施放");
            var from = player.Position;
            foreach (var to in ctx.TravelPath)
            {
                int dq = to.Q - from.Q, dr = to.R - from.R;
                if ((Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2 != 1
                    || ctx.MovementMap == null || !ctx.MovementMap.Placed.TryGetValue(to, out var tile) || tile.Edges.Length == 0)
                    throw new InvalidOperationException("地道路径必须逐格相邻且已揭示");
                if (tile.Edges[0] == TerrainType.Swamp || tile.Edges[0] == TerrainType.Lake)
                    throw new InvalidOperationException("地道不能进入或经过沼泽与湖泊");
                from = to;
            }
            if (ctx.MovementMap == null || !ctx.MovementMap.Placed.TryGetValue(from,out var destination) || destination.Edges.Length == 0)
                throw new InvalidOperationException("地道路径必须逐格相邻且已揭示");
            if (destination.Edges[0] == TerrainType.Swamp || destination.Edges[0] == TerrainType.Lake)
                throw new InvalidOperationException("地道不能进入或经过沼泽与湖泊");
            if (!_enh)
            {
                if (!CardTravelActions.IsSafeDestination(player, ctx, from, ctx.OtherPlayers))
                    throw new InvalidOperationException("地道穿行必须结束在安全格");
            }
            else
            {
                bool occupied = ctx.OtherPlayers.Any(p => !ReferenceEquals(p,player) && p.Position.Equals(from));
                bool fort = ctx.MovementMap != null && ctx.MovementMap.Places.TryGetValue(from,out var place)
                    && (place.Type == PlaceType.Keep || place.Type == PlaceType.City || place.Type == PlaceType.MageTower) && !place.Conquered;
                if (!occupied && !fort) throw new InvalidOperationException("地道攻击必须结束在可突袭的城防地点或其他玩家格");
                if (!occupied && ctx.MovementMap.Places[from].GuardNeedsReplacement
                    && (ctx.EnemySupply == null || !ctx.EnemySupply.CanDraw(EnemyTokenSupply.GuardColor(ctx.MovementMap.Places[from].Type))))
                    throw new InvalidOperationException("缺少可用于再次突袭的实际守军标记");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var origin = player.Position;
            var target = ctx.TravelPath.Count > 0 ? ctx.TravelPath.Last() : origin;
            if (_enh && ctx.MovementMap.Places.TryGetValue(target, out var targetSite) && targetSite.GuardNeedsReplacement
                && !ctx.OtherPlayers.Any(p => !ReferenceEquals(p,player) && p.Position.Equals(target)))
            {
                targetSite.Enemy = ctx.EnemySupply.Draw(EnemyTokenSupply.GuardColor(targetSite.Type));
                targetSite.GuardNeedsReplacement = false;
            }
            if (ctx.TravelPath.Count > 0) player.Position = ctx.TravelPath.Last();
            ctx.CurrentTerrain = ctx.MovementMap.Placed[player.Position].Edges[0];
            ctx.TravelPath.Clear();
            if (_enh)
            {
                ctx.TunnelOrigin = origin;
                ctx.TunnelPreviousSiteIgnore = ctx.IgnoreSiteFortifications;
                ctx.TunnelAssaultActive = true;
                ctx.TunnelTargetPlayer = ctx.OtherPlayers.FirstOrDefault(p => !ReferenceEquals(p,player) && p.Position.Equals(player.Position));
                ctx.TurnActionCommitted = ctx.InBattle = true;
                ctx.MovementMap.Places.TryGetValue(player.Position, out var place);
                ctx.TunnelTargetSite = ctx.TunnelTargetPlayer == null ? place : null;
                ctx.CombatSite = ctx.TunnelTargetSite;
                ctx.TunnelDestination = player.Position;
                ctx.BeginTunnelDefenders(ctx.TunnelTargetSite?.Defenders);
                if (place?.Type == PlaceType.Keep || place?.Type == PlaceType.MageTower)
                    player.Reputation = Math.Max(-7, player.Reputation - 1);
                ctx.FortifiedSite = place?.Type == PlaceType.Keep || place?.Type == PlaceType.City || place?.Type == PlaceType.MageTower;
                ctx.Enemies = ctx.TunnelDefenders.ToList();
                ctx.EngagedEnemies = ctx.Enemies.Count;
                ctx.IgnoreSiteFortifications = true;
                ctx.ReturnAfterRetreat = true;
            }
        }
    }
}
