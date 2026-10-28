using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloWoundZone { None, Hand, Discard }
    public sealed record SoloEndTurnInput(int[] DiscardHandSlots, SoloWoundZone WoundZone = SoloWoundZone.None,
        int WoundSlot = -1, ManaColor? MineColor = null, int SpellRewardSlot = -1,
        int ArtifactRewardSlot=-1,ManaColor[] GoldRewardColors=null,SoloRuinRewardInput RuinReward=null,bool CommitRewards=false);

    public sealed partial class PveJourneySession
    {
        public SoloEndTurnInput CommittedRewardEnd { get; internal set; }
        internal void FinishCommittedRewardEnd()
        {
            Game.GrantPendingEndRewards(Context);CommittedRewardEnd=null;
            Progression?.PrepareNext();if(Progression?.Pending!=true)CompleteTurn();
        }
    }

    /// <summary>One complete native end-turn choice. Physical slots refer to the immutable
    /// pre-action support snapshot, never to a global card identifier. No hand-size cap.</summary>
    public static class SoloEndTurnInputs
    {
        public static IEnumerable<SoloEndTurnInput> Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;var p=j.Player;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn&&j.CommittedRewardEnd==null || j.Context==null || j.Context.InBattle)
                yield break;
            var ordinary=Enumerable.Range(0,p.Deck.Hand.Count).Where(i=>p.Deck.Hand[i].Type!=CardType.Wound).ToArray();
            var site=SiteTurnRules.At(j.Game.Map,p);
            var wounds=new List<(SoloWoundZone zone,int slot)>{(SoloWoundZone.None,-1)};
            if(site?.Type==PlaceType.MagicalGlade)
            {
                wounds.AddRange(Enumerable.Range(0,p.Deck.Hand.Count).Where(i=>p.Deck.Hand[i].Type==CardType.Wound).Select(i=>(SoloWoundZone.Hand,i)));
                wounds.AddRange(Enumerable.Range(0,p.Deck.DiscardPile.Count).Where(i=>p.Deck.DiscardPile[i].Type==CardType.Wound).Select(i=>(SoloWoundZone.Discard,i)));
            }
            var colors=site?.Type is PlaceType.ManaMine or PlaceType.DeepMine
                ? (site.MineColors??Array.Empty<ManaColor>()).Distinct().Select(c=>(ManaColor?)c).ToArray()
                :new ManaColor?[]{null};
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            var spellSlots=j.Context.PendingSpellReward
                ?Enumerable.Range(0,j.Game.Globals.Spells.Offer.Count).ToArray():new[]{-1};
            int artifactCount=SoloAdventureRewards.ArtifactCount(j);
            var artifactSlots=artifactCount>0?Enumerable.Range(0,artifactCount).ToArray():new[]{-1};
            var goldChoices=SoloAdventureRewards.GoldChoices(SoloAdventureRewards.GoldSlots(j).Length).ToArray();
            bool needsReveal=SoloAdventureRewards.NeedsArtifactReveal(j);
            if(needsReveal){spellSlots=new[]{-1};goldChoices=new[]{Array.Empty<ManaColor>()};}
            var ruinChoices=needsReveal?new SoloRuinRewardInput[]{null}:SoloRuinRewardInputs.Choices(j).ToArray();
            var locked=j.CommittedRewardEnd;
            foreach(var discard in locked==null?Subsets(ordinary,0,new List<int>()):new[]{locked.DiscardHandSlots})
            foreach(var wound in locked==null?wounds:new List<(SoloWoundZone zone,int slot)>{(locked.WoundZone,locked.WoundSlot)})
            foreach(var color in locked==null?colors:new[]{locked.MineColor})
            foreach(var spellSlot in spellSlots)
            foreach(var artifactSlot in artifactSlots)
            foreach(var goldColors in goldChoices)
            foreach(var ruin in ruinChoices)
            {
                var input=needsReveal?new SoloEndTurnInput(discard,wound.zone,wound.slot,color,CommitRewards:true)
                    :new SoloEndTurnInput(discard,wound.zone,wound.slot,color,spellSlot,artifactSlot,goldColors,ruin);
                if(j.CommittedRewardEnd!=null&&!SameCommittedChoices(j.CommittedRewardEnd,input))continue;
                bool valid=false;
                try { Apply(episode,input);valid=true; }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
                if(valid)yield return input; // state is restored before returning control
            }
        }

        private static IEnumerable<int[]> Subsets(int[] slots,int at,List<int> chosen)
        {
            if(at==slots.Length){yield return chosen.ToArray();yield break;}
            foreach(var subset in Subsets(slots,at+1,chosen))yield return subset;
            chosen.Add(slots[at]);
            foreach(var subset in Subsets(slots,at+1,chosen))yield return subset;
            chosen.RemoveAt(chosen.Count-1);
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloEndTurnInput input)
        {
            if(episode==null || input==null || input.DiscardHandSlots==null)throw new ArgumentNullException();
            return episode.Apply(j=>Execute(j,input));
        }
        private static bool SameCommittedChoices(SoloEndTurnInput a,SoloEndTurnInput b)
            =>a.DiscardHandSlots.SequenceEqual(b.DiscardHandSlots)&&a.WoundZone==b.WoundZone
                &&a.WoundSlot==b.WoundSlot&&a.MineColor==b.MineColor;

        /// <summary>Used inside the episode transaction: reward selection and its formal consumer
        /// are one operation, so the policy cannot farm reversible reward preselection.</summary>
        public static void Execute(PveJourneySession j,SoloEndTurnInput input)
        {
                if(j==null || input==null || input.DiscardHandSlots==null)throw new ArgumentNullException();
                var p=j.Player;
                if(j.CommittedRewardEnd!=null)
                {
                    if(input.CommitRewards||!SameCommittedChoices(j.CommittedRewardEnd,input))
                        throw new InvalidOperationException("Cannot revise end-turn choices after cleanup and reveal.");
                    SelectRewards(j,input);j.FinishCommittedRewardEnd();return;
                }
                if(input.DiscardHandSlots.Distinct().Count()!=input.DiscardHandSlots.Length
                    ||input.DiscardHandSlots.Any(i=>i<0 || i>=p.Deck.Hand.Count))
                    throw new InvalidOperationException("Invalid physical discard slots.");
                DeedCard wound=null;
                switch(input.WoundZone)
                {
                    case SoloWoundZone.None:
                        if(input.WoundSlot!=-1)throw new InvalidOperationException("Absent wound must not have a slot.");break;
                    case SoloWoundZone.Hand:
                        if(input.WoundSlot<0 || input.WoundSlot>=p.Deck.Hand.Count)throw new InvalidOperationException("Invalid hand wound slot.");
                        wound=p.Deck.Hand[input.WoundSlot];break;
                    case SoloWoundZone.Discard:
                        if(input.WoundSlot<0 || input.WoundSlot>=p.Deck.DiscardPile.Count)throw new InvalidOperationException("Invalid discard wound slot.");
                        wound=p.Deck.DiscardPile[input.WoundSlot];break;
                    default:throw new ArgumentOutOfRangeException(nameof(input.WoundZone));
                }
                if(input.CommitRewards)
                {
                    if(j.CommittedRewardEnd!=null||!SoloAdventureRewards.NeedsArtifactReveal(j)
                        ||input.SpellRewardSlot!=-1||input.ArtifactRewardSlot!=-1
                        ||input.GoldRewardColors?.Length>0||input.RuinReward!=null)
                        throw new InvalidOperationException("End-turn commitment must precede any hidden artifact reward choice.");
                    j.Game.ValidateEndCurrentTurn(j.Context,input.DiscardHandSlots.Select(i=>p.Deck.Hand[i]),
                        new SiteTurnSelection{GladeWound=wound,MineColor=input.MineColor});
                    j.Context.ManaSource?.RequireSourceOpeningReturnChoices(p);
                    if(j.Context.ManaMeltdownOwner!=null||j.Context.TunnelAssaultActive
                        ||j.Context.TimeBendingCard!=null&&!p.Deck.PlayedCards.Any(c=>ReferenceEquals(c,j.Context.TimeBendingCard)))
                        throw new InvalidOperationException("Resolve outstanding turn obligations before committing rewards.");
                    j.Game.BeginEndCurrentTurn(j.Context,input.DiscardHandSlots.Select(i=>p.Deck.Hand[i]).ToArray(),
                        new SiteTurnSelection{GladeWound=wound,MineColor=input.MineColor},deferRewards:true);
                    j.CommittedRewardEnd=input with {DiscardHandSlots=(int[])input.DiscardHandSlots.Clone()};
                    SoloAdventureRewards.RevealEarned(j);
                    return;
                }
                if(SoloAdventureRewards.NeedsArtifactReveal(j))throw new InvalidOperationException("Commit the turn before revealing and selecting artifact rewards.");
                SelectRewards(j,input);
                j.EndTurn(input.DiscardHandSlots.Select(i=>p.Deck.Hand[i]).ToArray(),
                    new SiteTurnSelection{GladeWound=wound,MineColor=input.MineColor});
        }
        private static void SelectRewards(PveJourneySession j,SoloEndTurnInput input)
        {
                if(j.Context.PendingSpellReward)
                {
                    if(input.SpellRewardSlot<0 || input.SpellRewardSlot>=j.Game.Globals.Spells.Offer.Count)
                        throw new InvalidOperationException("Pending spell reward requires an actual offer slot.");
                    if(j.CommittedRewardEnd!=null)FortifiedSiteActions.SelectSpellReward(j.Context,input.SpellRewardSlot);
                    else j.SelectSiteSpellReward(j.Game.Globals.Spells.Offer[input.SpellRewardSlot]);
                }
                else if(input.SpellRewardSlot!=-1)throw new InvalidOperationException("No pending spell reward.");
                SoloAdventureRewards.Select(j,input.ArtifactRewardSlot,input.GoldRewardColors);
                SoloRuinRewardInputs.Select(j,input.RuinReward);
        }
    }
}
