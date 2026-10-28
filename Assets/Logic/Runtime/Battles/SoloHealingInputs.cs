using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>UnitSlot -1 spends healing on one actual hand wound; other slots select an owned unit.</summary>
    public sealed record SoloHealingInput(int UnitSlot = -1, bool GrailFirst = true);

    public static class SoloHealingInputs
    {
        public static IEnumerable<SoloHealingInput> Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null
                ||j.Context.InBattle || j.Context.Healing.Total<=0)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            for(int slot=-1;slot<j.Player.Units.Count;slot++)
            {
                // Preserve source priority only when both distinct healing pools exist.
                int priorities=j.Context.Healing.Ordinary>0 && j.Context.Healing.Grail>0?2:1;
                for(int priority=0;priority<priorities;priority++)
                {
                    var input=new SoloHealingInput(slot,priority==0);
                    bool valid=false;
                    try { Apply(episode,input);valid=true; }
                    catch(InvalidOperationException) { }
                    finally { checkpoint.Restore(); }
                    if(valid)yield return input;
                }
            }
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloHealingInput input)
        {
            if(episode==null || input==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                if(input.UnitSlot < -1 || input.UnitSlot>=j.Player.Units.Count)
                    throw new InvalidOperationException("Invalid actual healing unit slot.");
                j.HealOutsideBattle(input.UnitSlot<0?null:j.Player.Units[input.UnitSlot],input.GrailFirst);
            });
        }
    }
}
