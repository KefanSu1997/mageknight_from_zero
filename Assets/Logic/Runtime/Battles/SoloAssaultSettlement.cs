using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloAssaultSettlementInput(bool Withdraw);
    public static class SoloAssaultSettlement
    {
        public static SoloAssaultSettlementInput[] Legal(PveJourneySession journey)
        {
            if(journey==null)throw new ArgumentNullException(nameof(journey));
            if(journey.SoloClock?.ScenarioEnded==true || !journey.Game.HasActiveTurn
                ||journey.Game.IsEndingTurn || journey.Context?.TunnelAssaultActive!=true)
                return Array.Empty<SoloAssaultSettlementInput>();
            var checkpoint=DemoStateCheckpoint.Capture(journey);
            var legal=new List<SoloAssaultSettlementInput>();
            foreach(bool withdraw in new[]{false,true})
            {
                try { journey.FinishFortifiedAssault(withdraw);legal.Add(new(withdraw)); }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
            }
            return legal.ToArray();
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloAssaultSettlementInput input)
        {
            if(episode==null || input==null)throw new ArgumentNullException();
            return episode.Apply(j=>j.FinishFortifiedAssault(input.Withdraw));
        }
    }
}
