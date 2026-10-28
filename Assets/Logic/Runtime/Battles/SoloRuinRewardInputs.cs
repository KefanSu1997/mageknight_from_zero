using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloRuinRewardInput(int[] ArtifactSlots,int SpellSlot=-1,int AdvancedSlot=-1,
        int UnitSlot=-1,int DisbandSlot=-1,bool LoyaltySlot=false);
    public static class SoloRuinRewardInputs
    {
        public static IEnumerable<SoloRuinRewardInput> Choices(PveJourneySession j)
        {
            var r=j.Context?.Ruin;
            if(r==null||!r.Completed||r.PendingRewards.Count==0){yield return null;yield break;}
            var rewards=r.PendingRewards;
            int[] Slots(bool needed,int count)=>needed?Enumerable.Range(0,count).ToArray():new[]{-1};
            foreach(var artifacts in OrderedSlots(r.ArtifactChoices.Count,rewards.Count(x=>x==RuinReward.Artifact),new List<int>()))
            foreach(int spell in Slots(rewards.Contains(RuinReward.Spell),j.Context.SpellSupply.Offer.Count))
            foreach(int advanced in Slots(rewards.Contains(RuinReward.AdvancedAction),j.Context.AdvActionSupply.Offer.Count))
            foreach(int unit in Slots(rewards.Contains(RuinReward.Unit),j.Context.UnitOffer.Count))
            foreach(int disband in unit<0?new[]{-1}:Enumerable.Range(-1,j.Player.Units.Count+1))
            foreach(bool loyalty in unit<0?new[]{false}:new[]{false,true})
                yield return new(artifacts,spell,advanced,unit,disband,loyalty);
        }
        private static IEnumerable<int[]> OrderedSlots(int count,int needed,List<int> selected)
        {
            if(selected.Count==needed){yield return selected.ToArray();yield break;}
            for(int i=0;i<count;i++)if(!selected.Contains(i))
            {
                selected.Add(i);
                foreach(var row in OrderedSlots(count,needed,selected))yield return row;
                selected.RemoveAt(selected.Count-1);
            }
        }
        public static void Select(PveJourneySession j,SoloRuinRewardInput input)
        {
            var r=j.Context?.Ruin;
            if(r==null||!r.Completed||r.PendingRewards.Count==0)
            {
                if(input!=null)throw new InvalidOperationException("No pending ruin reward.");
                return;
            }
            if(input==null||input.ArtifactSlots==null)throw new InvalidOperationException("Complete ruin reward selection required.");
            var rewards=r.PendingRewards;
            int count=rewards.Count(x=>x==RuinReward.Artifact);
            if(input.ArtifactSlots.Length!=count||input.ArtifactSlots.Distinct().Count()!=count
                ||input.ArtifactSlots.Any(i=>i<0||i>=r.ArtifactChoices.Count))
                throw new InvalidOperationException("Invalid ordered physical artifact reward slots.");
            void Slot(RuinReward kind,int slot,Action<int> select)
            {
                if(rewards.Contains(kind))select(slot);
                else if(slot!=-1)throw new InvalidOperationException("Extraneous ruin reward selection.");
            }
            if(count>0)r.SelectArtifacts(input.ArtifactSlots.Select(i=>r.ArtifactChoices[i]).ToArray());
            Slot(RuinReward.Spell,input.SpellSlot,r.SelectSpell);
            Slot(RuinReward.AdvancedAction,input.AdvancedSlot,r.SelectAdvancedAction);
            if(!rewards.Contains(RuinReward.Unit)&&(input.DisbandSlot!=-1||input.LoyaltySlot))
                throw new InvalidOperationException("Unit parameters without a unit reward.");
            Slot(RuinReward.Unit,input.UnitSlot,index=>
            {
                if(input.DisbandSlot< -1||input.DisbandSlot>=j.Player.Units.Count)
                    throw new InvalidOperationException("Invalid dismissed unit slot.");
                r.SelectUnit(index,input.DisbandSlot<0?null:j.Player.Units[input.DisbandSlot],input.LoyaltySlot);
            });
        }
    }
}
