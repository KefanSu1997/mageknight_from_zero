using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloSiteOperation { Recruit, BuySpell, BuyHealing, Dismiss, EndInteraction, BuyAdvanced, BuyBlindAdvanced, ExpandEliteOffer, BuyArtifact, LearnMonastery, BuyMonasteryHealing }
    public sealed record SoloSiteInput(SoloSiteOperation Operation,int Slot=-1,
        ManaColor? FamiliarMana=null,bool Loyalty=false,int CommandBannerPlayedSlot=-1);

    /// <summary>Actual site purchases; opening negotiation and its first purchase are atomic.
    /// Command banner selection and recruitment are committed together.</summary>
    public static class SoloSiteInputs
    {
        public static IEnumerable<SoloSiteInput> Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null
                || j.Context.InBattle || !j.CanInteractAtCurrentSite)yield break;
            var proposals=new List<SoloSiteInput>();
            for(int i=0;i<j.Game.Globals.Units.Offer.Count;i++)
                foreach(bool loyalty in new[]{false,true})
                foreach(int banner in new[]{-1}.Concat(Enumerable.Range(0,j.Player.Deck.PlayedCards.Count)
                    .Where(k=>!loyalty&&BannerActions.CommandAvailable(j.Player,j.Context,j.Player.Deck.PlayedCards[k]))))
                {
                    proposals.Add(new(SoloSiteOperation.Recruit,i,Loyalty:loyalty,CommandBannerPlayedSlot:banner));
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
                        proposals.Add(new(SoloSiteOperation.Recruit,i,color,loyalty,banner));
                }
            for(int i=0;i<j.Game.Globals.Spells.Offer.Count;i++)proposals.Add(new(SoloSiteOperation.BuySpell,i));
            for(int i=0;i<j.Game.Globals.AdvActions.Offer.Count;i++)proposals.Add(new(SoloSiteOperation.BuyAdvanced,i));
            proposals.Add(new(SoloSiteOperation.BuyBlindAdvanced));
            proposals.Add(new(SoloSiteOperation.ExpandEliteOffer));
            proposals.Add(new(SoloSiteOperation.BuyArtifact));
            proposals.Add(new(SoloSiteOperation.BuyMonasteryHealing));
            for(int i=0;i<j.Game.Globals.MonasteryOffer.Count;i++)proposals.Add(new(SoloSiteOperation.LearnMonastery,i));
            proposals.Add(new(SoloSiteOperation.BuyHealing));
            for(int i=0;i<j.Player.Units.Count;i++)proposals.Add(new(SoloSiteOperation.Dismiss,i));
            if(j.Context.InNegotiation)proposals.Add(new(SoloSiteOperation.EndInteraction));
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            foreach(var input in proposals)
            {
                bool valid=false;
                try { Apply(episode,input);valid=true; }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
                if(valid)yield return input;
            }
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloSiteInput input)
        {
            if(episode==null || input==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                if(!Enum.IsDefined(typeof(SoloSiteOperation),input.Operation))throw new InvalidOperationException("Unknown site operation.");
                if(input.CommandBannerPlayedSlot < -1)throw new InvalidOperationException("Invalid command banner slot.");
                if(input.Operation!=SoloSiteOperation.Recruit && (input.FamiliarMana.HasValue || input.Loyalty||input.CommandBannerPlayedSlot!=-1))
                    throw new InvalidOperationException("Recruitment parameters require a recruitment operation.");
                bool noSlot=input.Operation==SoloSiteOperation.BuyHealing || input.Operation==SoloSiteOperation.EndInteraction||input.Operation==SoloSiteOperation.BuyMonasteryHealing
                    ||input.Operation==SoloSiteOperation.BuyBlindAdvanced||input.Operation==SoloSiteOperation.ExpandEliteOffer||input.Operation==SoloSiteOperation.BuyArtifact;
                if(noSlot && input.Slot!=-1)throw new InvalidOperationException("This operation has no target slot.");
                if(input.Operation==SoloSiteOperation.EndInteraction)
                {j.EndSiteInteraction();return;}
                if(!j.Context.InNegotiation)j.BeginSiteInteraction();
                switch(input.Operation)
                {
                    case SoloSiteOperation.Recruit:
                        if(input.Slot<0 || input.Slot>=j.Game.Globals.Units.Offer.Count)throw new InvalidOperationException("Invalid unit offer slot.");
                        if(input.FamiliarMana.HasValue!=j.Game.Globals.Units.Offer[input.Slot].RequiresAttunement)
                            throw new InvalidOperationException("Mana color is required only for actual attunement units.");
                        j.SelectLoyaltyRecruit(input.Loyalty);
                        if(input.CommandBannerPlayedSlot>=j.Player.Deck.PlayedCards.Count)
                            throw new InvalidOperationException("Invalid actual played banner slot.");
                        if(input.Loyalty&&input.CommandBannerPlayedSlot>=0)
                            throw new InvalidOperationException("Loyalty and command banner cannot share a recruit.");
                        j.SelectCommandBanner(input.CommandBannerPlayedSlot<0?null:j.Player.Deck.PlayedCards[input.CommandBannerPlayedSlot]);
                        j.RecruitSiteUnit(j.Game.Globals.Units.Offer[input.Slot],input.FamiliarMana);break;
                    case SoloSiteOperation.BuySpell:
                        if(input.Slot<0 || input.Slot>=j.Game.Globals.Spells.Offer.Count)throw new InvalidOperationException("Invalid spell offer slot.");
                        j.BuySiteSpell(j.Game.Globals.Spells.Offer[input.Slot]);break;
                    case SoloSiteOperation.BuyHealing:j.BuyVillageHealing(1);break;
                    case SoloSiteOperation.BuyAdvanced:
                        if(input.Slot<0||input.Slot>=j.Game.Globals.AdvActions.Offer.Count)
                            throw new InvalidOperationException("Invalid actual advanced offer slot.");
                        j.BuyGreenCityAdvanced(j.Game.Globals.AdvActions.Offer[input.Slot]);break;
                    case SoloSiteOperation.BuyBlindAdvanced:j.BuyGreenCityAdvanced(blind:true);break;
                    case SoloSiteOperation.ExpandEliteOffer:j.ExpandWhiteCityUnitOffer();break;
                    case SoloSiteOperation.BuyArtifact:j.BuyRedCityArtifact();break;
                    case SoloSiteOperation.BuyMonasteryHealing:j.BuyMonasteryHealing();break;
                    case SoloSiteOperation.LearnMonastery:
                        if(input.Slot<0||input.Slot>=j.Game.Globals.MonasteryOffer.Count)throw new InvalidOperationException("Invalid monastery offer slot.");
                        j.LearnMonasteryAdvanced(j.Game.Globals.MonasteryOffer[input.Slot]);break;
                    case SoloSiteOperation.Dismiss:
                        if(input.Slot<0 || input.Slot>=j.Player.Units.Count)throw new InvalidOperationException("Invalid owned unit slot.");
                        j.DismissSiteUnit(j.Player.Units[input.Slot]);break;
                }
            });
        }
    }
}
