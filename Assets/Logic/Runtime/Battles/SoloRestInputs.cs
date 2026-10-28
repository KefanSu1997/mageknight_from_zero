using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloRestInput(int OrdinaryHandSlot,int[] WoundHandSlots);

    /// <summary>Complete standard-rest or slow-recovery choice over physical hand slots.</summary>
    public static class SoloRestInputs
    {
        public static IEnumerable<SoloRestInput> Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null
                ||j.Context.InBattle || j.Context.MovedThisTurn || j.Context.TurnActionCommitted || j.Context.RestedThisTurn)
                yield break;
            var hand=j.Player.Deck.Hand;
            var ordinary=Enumerable.Range(0,hand.Count).Where(i=>hand[i].Type!=CardType.Wound).ToArray();
            var wounds=Enumerable.Range(0,hand.Count).Where(i=>hand[i].Type==CardType.Wound).ToArray();
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            var candidates=ordinary.Length==0?wounds.Select(i=>new SoloRestInput(-1,new[]{i}))
                :ordinary.SelectMany(i=>WoundSubsets(wounds,0,new List<int>()).Select(w=>new SoloRestInput(i,w)));
            foreach(var candidate in candidates)
            {
                bool valid=false;
                try { Apply(episode,candidate);valid=true; }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
                if(valid)yield return candidate;
            }
        }

        private static IEnumerable<int[]> WoundSubsets(int[] slots,int at,List<int> chosen)
        {
            if(at==slots.Length){yield return chosen.ToArray();yield break;}
            foreach(var value in WoundSubsets(slots,at+1,chosen))yield return value;
            chosen.Add(slots[at]);
            foreach(var value in WoundSubsets(slots,at+1,chosen))yield return value;
            chosen.RemoveAt(chosen.Count-1);
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloRestInput input)
        {
            if(episode==null || input==null || input.WoundHandSlots==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                var hand=j.Player.Deck.Hand;
                if(input.OrdinaryHandSlot < -1 || input.OrdinaryHandSlot>=hand.Count
                    ||input.WoundHandSlots.Distinct().Count()!=input.WoundHandSlots.Length
                    ||input.WoundHandSlots.Any(i=>i<0 || i>=hand.Count))
                    throw new InvalidOperationException("Invalid actual rest card slots.");
                j.Rest(input.OrdinaryHandSlot<0?null:hand[input.OrdinaryHandSlot],
                    input.WoundHandSlots.Select(i=>hand[i]).ToArray());
            });
        }
    }
}
