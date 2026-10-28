using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public static class SoloAdventureRewards
    {
        public static bool NeedsArtifactReveal(PveJourneySession j)
        {
            var d=j.Context?.Dungeon;var l=j.Context?.Lair;var r=j.Context?.Ruin;
            return r?.Completed==true&&r.PendingRewards.Contains(RuinReward.Artifact)&&r.ArtifactChoices.Count==0
                ||d?.Completed==true&&!d.RewardGranted&&d.ArtifactChoices.Count==0
                    &&(d.Reward==DungeonRewardKind.Artifact||d.Reward==DungeonRewardKind.SpellAndArtifact)
                ||l?.Completed==true&&l.Won&&!l.RewardGranted&&l.Site.Type==PlaceType.SpawningGround&&l.ArtifactChoices.Count==0;
        }
        // Revealing an earned pair is mandatory, not a reversible policy preselection.
        public static void RevealEarned(PveJourneySession j)
        {
            if(j.CommittedRewardEnd==null)throw new InvalidOperationException("Commit end-turn choices before revealing artifact rewards.");
            var d=j.Context?.Dungeon;var l=j.Context?.Lair;
            var r=j.Context?.Ruin;
            if(r?.Completed==true&&r.PendingRewards.Contains(RuinReward.Artifact)&&r.ArtifactChoices.Count==0)
                r.RevealArtifacts();
            if(d?.Completed==true&&!d.RewardGranted&&d.ArtifactChoices.Count==0
                &&(d.Reward==DungeonRewardKind.Artifact||d.Reward==DungeonRewardKind.SpellAndArtifact))
                d.RevealArtifacts();
            if(l?.Completed==true&&l.Won&&!l.RewardGranted&&l.Site.Type==PlaceType.SpawningGround
                &&l.ArtifactChoices.Count==0)l.RevealArtifacts();
        }
        public static int ArtifactCount(PveJourneySession j)
        {
            var d=j.Context?.Dungeon;var l=j.Context?.Lair;
            return d?.Completed==true&&!d.RewardGranted?d.ArtifactChoices.Count:
                l?.Completed==true&&!l.RewardGranted?l.ArtifactChoices.Count:0;
        }
        public static int[] GoldSlots(PveJourneySession j)=>j.Context?.Lair is LairActions l
            &&l.Completed&&l.Won&&!l.RewardGranted
            ?Enumerable.Range(0,l.RewardDice.Count).Where(i=>l.RewardDice[i]==ManaColor.Gold).ToArray():Array.Empty<int>();
        public static IEnumerable<ManaColor[]> GoldChoices(int count)
        {
            if(count==0){yield return Array.Empty<ManaColor>();yield break;}
            foreach(var tail in GoldChoices(count-1))
            foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
                yield return new[]{c}.Concat(tail).ToArray();
        }
        public static void Select(PveJourneySession j,int artifactSlot,ManaColor[] goldColors)
        {
            int count=ArtifactCount(j);var slots=GoldSlots(j);var colors=goldColors??Array.Empty<ManaColor>();
            if(colors.Length!=slots.Length||colors.Any(c=>c!=ManaColor.Red&&c!=ManaColor.Blue&&c!=ManaColor.Green&&c!=ManaColor.White))
                throw new InvalidOperationException("Reward colors must match all actual gold dice.");
            if(count==0&&artifactSlot!=-1||count>0&&(artifactSlot<0||artifactSlot>=count))
                throw new InvalidOperationException("Artifact reward must identify an actual revealed slot.");
            if(count>0)
            {
                if(j.Context.Dungeon?.Completed==true&&!j.Context.Dungeon.RewardGranted)
                    j.Context.Dungeon.SelectArtifact(j.Context.Dungeon.ArtifactChoices[artifactSlot]);
                else j.Context.Lair.SelectArtifact(j.Context.Lair.ArtifactChoices[artifactSlot]);
            }
            for(int i=0;i<slots.Length;i++)j.Context.Lair.ChooseGoldCrystal(slots[i],colors[i]);
        }
    }
}
