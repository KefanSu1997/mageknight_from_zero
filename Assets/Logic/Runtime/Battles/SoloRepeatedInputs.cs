using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloRepeatedInput(string Choice,SoloDeedSelection Inputs=null,
        AxialCoord? TargetHex=null,TerrainType? TargetTerrain=null);
    public sealed record SoloRepeatedPublic(string CardId,bool Enhanced,int Completed,int Remaining);
    public static class SoloRepeatedInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloRepeatedInput input)
            =>episode.Apply(j=>
            {
                if(j.Context.PendingRepeatedEffect==null)throw new InvalidOperationException("No committed repeated effect.");
                var hex=j.Context.TargetHex;var terrain=j.Context.TargetTerrain;
                try
                {
                    j.Context.TargetHex=input.TargetHex;j.Context.TargetTerrain=input.TargetTerrain;
                    PveCopyInputs.Continue(j.Player,j.Context,input.Choice,input.Inputs?.Resolve(j));
                }
                finally {j.Context.TargetHex=hex;j.Context.TargetTerrain=terrain;}
            });
        public static IEnumerable<SoloRepeatedInput> Legal(SoloConquestEpisode episode)
        {
            var pending=episode.Journey.Context?.PendingRepeatedEffect;
            if(episode.Terminal||pending==null)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            foreach(var input in SoloDeedInputs.CopiedChoices(episode.Journey,pending.OriginalCardId,pending.Enhanced))
            {
                bool valid=false;
                try {Apply(episode,input);valid=true;}
                catch(InvalidOperationException) { }
                finally {boundary.Restore();}
                if(valid)yield return input;
            }
        }
    }
}
