using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloSpatialKind { Move, Explore, Assault, ChallengeRampaging, Fly }
    public sealed record SoloSpatialCommand(SoloSpatialKind Kind, int Q, int R,int SkillSlot=-1);

    /// <summary>Spatial branch of the full scenario action space, not a complete policy.
    /// Commands contain public coordinates rather than a fixed opening-map slot number.
    /// Legal probing uses native transactions and restores the physical graph and RNG.
    /// Card/material, combat, rewards and clock choices must be merged by the full adapter.</summary>
    public static class SoloSpatialCommands
    {
        public static SoloSpatialCommand[] Legal(PveJourneySession journey, SoloConquestTileSupply tiles)
        {
            if(journey==null || tiles==null)throw new ArgumentNullException();
            if(journey.SoloClock?.ScenarioEnded==true || !journey.Game.HasActiveTurn
                || journey.Game.IsEndingTurn || journey.Context==null || journey.Context.InBattle)
                return Array.Empty<SoloSpatialCommand>();
            var proposals=new List<SoloSpatialCommand>();
            foreach(var coordinate in journey.Game.Map.Placed.Keys.OrderBy(c=>c.Q).ThenBy(c=>c.R))
            {
                proposals.Add(new(SoloSpatialKind.Move,coordinate.Q,coordinate.R));
                for(int skill=0;skill<journey.Player.Skills.Count;skill++)
                    if(journey.Player.Skills[skill].SourceId=="hero_skill_pool_005:06")
                        proposals.Add(new(SoloSpatialKind.Fly,coordinate.Q,coordinate.R,skill));
                if(journey.Game.Map.Places.ContainsKey(coordinate))
                {
                    proposals.Add(new(SoloSpatialKind.Assault,coordinate.Q,coordinate.R));
                    proposals.Add(new(SoloSpatialKind.ChallengeRampaging,coordinate.Q,coordinate.R));
                }
            }
            // Exploration validity depends on the public back, not the unrevealed tile face.
            // Do not draw a tile just to decide whether exploration is offered.
            var legal=journey.OriginalExplorationPositions(tiles)
                .Select(c=>new SoloSpatialCommand(SoloSpatialKind.Explore,c.Q,c.R)).ToList();
            var checkpoint=DemoStateCheckpoint.Capture(journey,tiles);
            foreach(var command in proposals)
            {
                try { Execute(journey,tiles,command);legal.Add(command); }
                catch(InvalidOperationException) { /* Native rule rejected this complete command. */ }
                finally { checkpoint.Restore(); }
            }
            return legal.OrderBy(c=>c.Kind).ThenBy(c=>c.Q).ThenBy(c=>c.R).ToArray();
        }

        public static void Execute(PveJourneySession journey, SoloConquestTileSupply tiles, SoloSpatialCommand command)
        {
            if(journey==null || tiles==null || command==null)throw new ArgumentNullException();
            if(journey.SoloClock?.ScenarioEnded==true)throw new InvalidOperationException("Scenario ended.");
            if(command.Kind!=SoloSpatialKind.Fly&&command.SkillSlot!=-1)
                throw new InvalidOperationException("Only flight uses an owned skill slot.");
            var target=new AxialCoord(command.Q,command.R);
            switch(command.Kind)
            {
                case SoloSpatialKind.Move:journey.Move(target);break;
                case SoloSpatialKind.Explore:journey.ExploreOriginal(tiles,target);break;
                case SoloSpatialKind.Assault:journey.AssaultFortifiedSite(target);break;
                case SoloSpatialKind.ChallengeRampaging:journey.ChallengeRampaging(target);break;
                case SoloSpatialKind.Fly:
                    if(command.SkillSlot<0||command.SkillSlot>=journey.Player.Skills.Count)
                        throw new InvalidOperationException("Invalid owned flight skill.");
                    journey.Fly(journey.Player.Skills[command.SkillSlot],target);break;
                default:throw new ArgumentOutOfRangeException(nameof(command.Kind));
            }
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode, SoloSpatialCommand command)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            return episode.Apply(journey=>Execute(journey,episode.Tiles,command));
        }
    }
}
