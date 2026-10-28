using MK.Logic.Runtime.Map;
using System;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    // Information earned by an actual scout activation, not a view of the hidden supply.
    public sealed record SoloScoutKnowledge(string EnemyId,string EnemyColor,int? Q,int? R,
        int? GuardSlot,string RuinToken,PublicRuinDetails RuinDetails);
    public sealed record SoloPublicWorldState(int Round, string DayPart, int PlayerQ, int PlayerR,
        PublicMapCell[] Cells, SoloTileSupplyPublic Tiles, SoloDummyPublicState Dummy,
        string PlayerTactic, string DummyTactic, string[] RemovedTactics,
        bool ActiveTurn, bool EndingTurn, bool PendingTacticInput, bool PlayerAnnounced,
        bool ScenarioEnded, AxialCoord[] ExplorationPositions)
    {
        public SoloScoutKnowledge LastScouting { get; init; }
    }

    public sealed partial class PveJourneySession
    {
        /// <summary>World branch only; combine with existing structured player/combat input.
        /// Never serialize the journey, tile checkpoint or dummy checkpoint as policy input.</summary>
        public SoloPublicWorldState ObserveSoloWorld(SoloConquestTileSupply supply)
        {
            if(SoloClock==null || _soloDummy==null || supply==null)
                throw new InvalidOperationException("Actual solo world and clock required.");
            var endingPart=Game.Clock.RoundIndex>6?MK.Logic.Core.DayPart.Night:Game.Clock.DayPart;
            return new SoloPublicWorldState(Math.Min(Game.Clock.RoundIndex,6),endingPart.ToString(),
                Player.Position.Q,Player.Position.R,PublicOriginalMap.Observe(Game.Map),supply.Observe(),_soloDummy.Observe(),
                SoloClock.PlayerTactic?.Id,SoloClock.DummyTactic?.Id,SoloClock.RemovedTactics.OrderBy(x=>x).ToArray(),
                Game.HasActiveTurn,Game.IsEndingTurn,Game.HasPendingTacticSelection,SoloClock.PlayerAnnouncedThisRound,
                SoloClock.ScenarioEnded,OriginalExplorationPositions(supply).ToArray())
                { LastScouting=ObserveSoloScouting() };
        }

        private SoloScoutKnowledge ObserveSoloScouting()
        {
            var observation=Context?.LastScoutObservation;
            if(observation==null)return null;
            int? q=null,r=null,slot=null;
            if(observation.Site!=null)
            {
                foreach(var pair in Game.Map.Places)
                    if(ReferenceEquals(pair.Value,observation.Site)){q=pair.Key.Q;r=pair.Key.R;break;}
                if(observation.Enemy!=null)
                    for(int i=0;i<observation.Site.Defenders.Count;i++)
                        if(ReferenceEquals(observation.Site.Defenders[i],observation.Enemy)){slot=i;break;}
            }
            var ruin=observation.Ruin.HasValue?observation.Site?.OriginalRuinToken:null;
            return new SoloScoutKnowledge(observation.Enemy?.Id,observation.Enemy?.TokenColor.ToString(),
                q,r,slot,observation.Ruin?.ToString(),ruin==null?null:
                    new PublicRuinDetails(ruin.Id,ruin.ManaCost.Select(c=>c.ToString()).ToArray(),ruin.Fame,
                        ruin.Enemies.Select(c=>c.ToString()).ToArray(),ruin.Rewards.Select(x=>x.ToString()).ToArray()));
        }

        public System.Collections.Generic.IReadOnlyList<AxialCoord> OriginalExplorationPositions(SoloConquestTileSupply supply)
        {
            if(supply==null)throw new ArgumentNullException(nameof(supply));
            if(!Game.HasActiveTurn || Game.IsEndingTurn || Context==null
                || !Game.Map.OriginalTiles.TryGetValue("A",out var start))return Array.Empty<AxialCoord>();
            return OriginalExplorationTransaction.LegalPositions(Game.Map,supply,Player,Context,start.Anchor);
        }

        public void InitializeSoloOriginalMap(PrintedTileCatalog catalog, SoloConquestTileSupply supply)
        {
            if(catalog==null||supply==null)throw new ArgumentNullException();
            var map=Game.Map;
            if(Game.HasActiveTurn||map.OriginalTiles.Count!=0||map.OriginalCells.Count!=0||map.Placed.Count!=0||map.Places.Count!=0
                ||supply.Capture().Revealed.Length!=0)
                throw new InvalidOperationException("Solo map setup requires an empty map and untouched supply before play");
            var start=catalog.Tiles["A"];
            var portal=start.Cells.Single(c=>c.Site=="Portal");
            var checkpoint=DemoStateCheckpoint.Capture(map,supply,Game.Globals,Player);
            try
            {
                OriginalTileMapPlacement.Place(map,start,new AxialCoord(0,0),SoloPrintedSiteFactory.Prepare(start,map,Game.Globals));
                foreach(var anchor in new[]{new AxialCoord(1,-3),new AxialCoord(3,-2)})
                {
                    if(supply.Observe().VisibleBack!="Countryside")throw new InvalidOperationException("Opening must use countryside tiles");
                    var tile=supply.Draw();
                    OriginalTileMapPlacement.Place(map,tile,anchor,SoloPrintedSiteFactory.Prepare(tile,map,Game.Globals));
                }
                Player.Position=portal.Local;
                SiteVisibility.RevealVisible(Player,map,Game.Clock.DayPart);
            }
            catch{checkpoint.Restore();throw;}
        }

        public PrintedTileDefinition ExploreOriginal(SoloConquestTileSupply supply, AxialCoord target)
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            var starting = Game.Map.OriginalTiles["A"];
            var tile = OriginalExplorationTransaction.Explore(Game.Map, supply, Player, Context,
                starting.Anchor, target, definition => SoloPrintedSiteFactory.Prepare(definition, Game.Map, Game.Globals),
                Game.Globals);
            SiteVisibility.RevealVisible(Player, Game.Map, Context.DayPart, Context);
            return tile;
        }
    }
}
