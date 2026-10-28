using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloLearningInput(int OfferSlot,bool Enhanced);
    public static class SoloLearningInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloLearningInput input)
            =>episode.Apply(j=>
            {
                if(input.OfferSlot<0||input.OfferSlot>=j.Game.Globals.AdvActions.Offer.Count)
                    throw new InvalidOperationException("Invalid actual learning offer slot.");
                RepeatedEffectResolution.RequireCompleted(j.Context);
                j.PurchaseLearnedCard(j.Game.Globals.AdvActions.Offer[input.OfferSlot],input.Enhanced);
            });
        public static IEnumerable<SoloLearningInput> Legal(SoloConquestEpisode episode)
        {
            var j=episode.Journey;
            if(episode.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn||j.Context==null
                ||j.Context.LearningModes==0||j.Context.LearnUsed)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int i=0;i<j.Game.Globals.AdvActions.Offer.Count;i++)
            foreach(bool enhanced in new[]{false,true})
            {
                var input=new SoloLearningInput(i,enhanced);bool valid=false;
                try {Apply(episode,input);valid=true;}
                catch(InvalidOperationException) { }
                finally {boundary.Restore();}
                if(valid)yield return input;
            }
        }
    }
}
