using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloClockKind { SelectTactic, BeginTurn, AnnounceRoundEnd, EndRound }
    public sealed record SoloClockCommand(SoloClockKind Kind, string TacticId = null);

    /// <summary>Clock branch. End-turn discard/site/reward choices belong to a separate
    /// complete-parameter command. Dummy turns are automatic, never selectable actions.</summary>
    public static class SoloClockCommands
    {
        public static SoloClockCommand[] Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            if(episode.Terminal)return Array.Empty<SoloClockCommand>();
            var journey=episode.Journey;
            var candidates=new List<SoloClockCommand>
            {
                new(SoloClockKind.BeginTurn),new(SoloClockKind.AnnounceRoundEnd),new(SoloClockKind.EndRound)
            };
            candidates.AddRange(journey.SoloClock.AvailableTactics()
                .Select(t=>new SoloClockCommand(SoloClockKind.SelectTactic,t.Id)));
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            var legal=new List<SoloClockCommand>();
            foreach(var command in candidates)
            {
                // Probe the complete episode transition, including automatic dummy scheduling.
                try { Apply(episode,command);legal.Add(command); }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
            }
            return legal.ToArray();
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode, SoloClockCommand command)
        {
            if(episode==null || command==null)throw new ArgumentNullException();
            if(command.Kind!=SoloClockKind.SelectTactic && command.TacticId!=null)
                throw new ArgumentException("Non-tactic commands must not carry a tactic id.");
            return episode.Apply(journey=>
            {
                switch(command.Kind)
                {
                    case SoloClockKind.SelectTactic:
                        if(command.TacticId==null || !journey.SoloClock.AvailableTactics().Any(t=>t.Id==command.TacticId))
                            throw new InvalidOperationException("Unavailable actual tactic.");
                        journey.SelectTactic(command.TacticId);break;
                    case SoloClockKind.BeginTurn:journey.BeginTurn();break;
                    case SoloClockKind.AnnounceRoundEnd:journey.AnnounceRoundEnd();break;
                    case SoloClockKind.EndRound:journey.EndRound();break;
                    default:throw new ArgumentOutOfRangeException(nameof(command.Kind));
                }
            });
        }
    }
}
