using System;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloOfferSlot(int Slot,string Id);
    public sealed record SoloPublicOffers(SoloOfferSlot[] Units,SoloOfferSlot[] Spells,SoloOfferSlot[] AdvancedActions,
        SoloOfferSlot[] CommonSkills,SoloOfferSlot[] LevelSkills,bool PendingLevel,int PendingLevelNumber,
        bool InNegotiation,bool InteractionClosed,bool PendingSpellReward)
    {
        public SoloOfferSlot[] AdventureArtifacts { get; init; }=Array.Empty<SoloOfferSlot>();
        public string[] AdventureRewardDice { get; init; }=Array.Empty<string>();
        public SoloOfferSlot[] RuinArtifacts { get; init; }=Array.Empty<SoloOfferSlot>();
        public string[] RuinRewards { get; init; }=Array.Empty<string>();
        public int PurchasedCityArtifacts { get; init; }
        public SoloOfferSlot[] CityArtifacts { get; init; }=Array.Empty<SoloOfferSlot>();
        public SoloOfferSlot[] MonasteryActions { get; init; }=Array.Empty<SoloOfferSlot>();
        public SoloEndTurnInput CommittedRewardEnd { get; init; }
        public SoloRepeatedPublic RepeatedEffect { get; init; }
        public int LearningModes { get; init; }
        public bool LearningUsed { get; init; }
    }

    public sealed partial class PveJourneySession
    {
        /// <summary>Only face-up offers and actual pending own-skill choices; no supply decks.</summary>
        public SoloPublicOffers ObserveSoloOffers()
        {
            if(!IsSoloConquest)throw new InvalidOperationException("Solo conquest observation required.");
            SoloOfferSlot[] Slots(System.Collections.Generic.IEnumerable<string> ids)
                =>ids.Select((id,i)=>new SoloOfferSlot(i,id)).ToArray();
            var g=Game.Globals;var progress=Progression;
            return new SoloPublicOffers(Slots(g.Units.Offer.Select(x=>x.Id)),Slots(g.Spells.Offer.Select(x=>x.Id)),
                Slots(progress.Actions.Select(x=>x.Id)),Slots(progress.CommonSkills.Select(x=>x.SourceId)),
                Slots(progress.Choices.Select(x=>x.SourceId)),progress.Pending,progress.PendingLevel,
                Context?.InNegotiation??false,Context?.InteractionClosed??false,Context?.PendingSpellReward??false)
            {
                PurchasedCityArtifacts=PurchasedCityArtifacts,
                LearningModes=Context?.LearningModes??0,LearningUsed=Context?.LearnUsed??false,
                RepeatedEffect=Context?.PendingRepeatedEffect==null?null:new(Context.PendingRepeatedEffect.OriginalCardId,
                    Context.PendingRepeatedEffect.Enhanced,Context.PendingRepeatedEffect.Completed,Context.PendingRepeatedEffect.Remaining),
                CommittedRewardEnd=CommittedRewardEnd==null?null:CommittedRewardEnd with {DiscardHandSlots=(int[])CommittedRewardEnd.DiscardHandSlots.Clone()},
                MonasteryActions=Slots(g.MonasteryOffer.Select(x=>x.Id)),
                CityArtifacts=Slots(CityArtifactChoices.Select(x=>x.Id)),
                RuinArtifacts=Slots(Context?.Ruin?.Completed==true&&!Context.Ruin.RewardGranted
                    ?Context.Ruin.ArtifactChoices.Select(x=>x.Id):Array.Empty<string>()),
                RuinRewards=Context?.Ruin?.PendingRewards.Select(x=>x.ToString()).ToArray()??Array.Empty<string>(),
                AdventureArtifacts=Slots(Context?.Dungeon?.Completed==true&&!Context.Dungeon.RewardGranted
                    ?Context.Dungeon.ArtifactChoices.Select(x=>x.Id):Context?.Lair?.Completed==true&&!Context.Lair.RewardGranted
                    ?Context.Lair.ArtifactChoices.Select(x=>x.Id):Array.Empty<string>()),
                AdventureRewardDice=Context?.Lair?.Completed==true&&!Context.Lair.RewardGranted
                    ?Context.Lair.RewardDice.Select(x=>x.ToString()).ToArray():Array.Empty<string>()
            };
        }
    }
}
