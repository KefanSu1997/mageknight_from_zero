using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Battles;

namespace MK.Logic.Runtime.Map
{
    /// <summary>Atomic printed-map reveal. The session must validate its active turn before calling.
    /// Site initialization must use actual supplies included in revealStateRoots. Never expose
    /// the initializer, checkpoint or drawn tile to the policy before successful commitment.</summary>
    public static class OriginalExplorationTransaction
    {
        /// <summary>Complete exploration support from public state, before any hidden tile draw.</summary>
        public static IReadOnlyList<AxialCoord> LegalPositions(MapState map, SoloConquestTileSupply supply,
            PlayerState player, ActionContext context, AxialCoord origin)
        {
            if(map==null || supply==null || player==null || context==null)throw new ArgumentNullException();
            if(!ReferenceEquals(map,context.MovementMap) || context.InBattle || context.TurnActionCommitted
                || context.RestedThisTurn || context.PeacefulMomentAction || context.PendingRepeatedEffect!=null
                || context.MovementPool<ExplorationService.Cost(player,context))return Array.Empty<AxialCoord>();
            var visible=supply.Observe();
            if(visible.VisibleBack==null)return Array.Empty<AxialCoord>();
            int range=context.SpaceBendingActive || context.ScoutExplorationRange>=2?2:1;
            return OriginalTilePlacementRules.SoloExplorationPositions(map.OriginalTiles.Values.Select(t=>t.Anchor),
                origin,player.Position,visible.VisibleBack,visible.IsReserve,range);
        }

        public static PrintedTileDefinition Explore(MapState map, SoloConquestTileSupply supply,
            PlayerState player, ActionContext context, AxialCoord origin, AxialCoord target,
            Func<PrintedTileDefinition, IReadOnlyDictionary<AxialCoord, PlaceState>> initializeSites,
            params object[] revealStateRoots)
        {
            if (map == null || supply == null || player == null || context == null || initializeSites == null
                || revealStateRoots == null) throw new ArgumentNullException();
            if (!ReferenceEquals(map, context.MovementMap) || context.InBattle || context.TurnActionCommitted
                || context.RestedThisTurn || context.PeacefulMomentAction || context.PendingRepeatedEffect != null)
                throw new InvalidOperationException("Exploration requires the actual movement phase");
            int cost = ExplorationService.Cost(player, context);
            if (context.MovementPool < cost) throw new InvalidOperationException("Insufficient exploration movement");
            int range = context.SpaceBendingActive || context.ScoutExplorationRange >= 2 ? 2 : 1;
            var anchors = map.OriginalTiles.Values.Select(t => t.Anchor).ToArray();
            if (!LegalPositions(map,supply,player,context,origin).Contains(target))
                throw new InvalidOperationException("Illegal exploration position");
            // Includes physical token identities and supply RNG, not merely dictionary counts.
            var checkpoint = DemoStateCheckpoint.Capture(new object[] { map, supply, player, context }
                .Concat(revealStateRoots).ToArray());
            try
            {
                context.MovementPool -= cost;
                var tile = supply.DrawForExploration(anchors, origin, player.Position, target, range);
                var sites = initializeSites(tile);
                OriginalTileMapPlacement.Place(map, tile, target, sites);
                return tile;
            }
            catch
            {
                checkpoint.Restore();
                throw;
            }
        }
    }
}
