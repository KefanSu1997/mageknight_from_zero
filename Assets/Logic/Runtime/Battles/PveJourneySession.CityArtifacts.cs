using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloCityArtifactReward(int[] KeepSlots);
    public sealed partial class PveJourneySession
    {
        private int _purchasedCityArtifacts;
        private readonly List<ArtifactCard> _cityArtifactChoices=new();
        public int PurchasedCityArtifacts=>_purchasedCityArtifacts;
        public IReadOnlyList<ArtifactCard> CityArtifactChoices=>_cityArtifactChoices;
        public void BuyRedCityArtifact()
        {
            RequireSiteInteraction();
            var site=CurrentRecruitSite;
            if(site==null||!site.Conquered||site.Type!=PlaceType.City||site.CityColor!=CityColor.Red)
                throw new InvalidOperationException("Artifact purchase requires a conquered red city.");
            if(Context.InfluencePool<12)throw new InvalidOperationException("Artifact purchase requires twelve influence.");
            if(Game.Globals.Artifacts.Count<=_purchasedCityArtifacts)
                throw new InvalidOperationException("No remaining artifact to purchase.");
            Context.InfluencePool-=12;
            _purchasedCityArtifacts++;
        }
        private void RevealPurchasedCityArtifacts()
        {
            if(_purchasedCityArtifacts==0)return;
            if(!Game.IsEndingTurn||_cityArtifactChoices.Count!=0)
                throw new InvalidOperationException("City artifacts may only be revealed once after committing end turn.");
            int count=Math.Min(_purchasedCityArtifacts+1,Game.Globals.Artifacts.Count);
            if(count<_purchasedCityArtifacts)throw new InvalidOperationException("Purchased artifact supply changed.");
            for(int i=0;i<count;i++)_cityArtifactChoices.Add(Game.Globals.Artifacts.Draw());
        }
        public void ClaimPurchasedCityArtifacts(int[] keepSlots)
        {
            if(!Game.IsEndingTurn||_purchasedCityArtifacts==0||_cityArtifactChoices.Count==0
                ||keepSlots==null||keepSlots.Length!=_purchasedCityArtifacts
                ||keepSlots.Distinct().Count()!=keepSlots.Length
                ||keepSlots.Any(i=>i<0||i>=_cityArtifactChoices.Count))
                throw new InvalidOperationException("Select all purchased artifacts from the actual revealed choices.");
            // Slots are ordered top first. Returning the unchosen card does not shuffle the supply.
            foreach(int i in keepSlots.Reverse())Player.Deck.PutOnTop(new DeedCard(_cityArtifactChoices[i].Id,CardType.Artifact));
            for(int i=0;i<_cityArtifactChoices.Count;i++)if(!keepSlots.Contains(i))Game.Globals.Artifacts.PutUnder(_cityArtifactChoices[i]);
            _cityArtifactChoices.Clear();_purchasedCityArtifacts=0;
            Progression?.PrepareNext();
            if(Progression?.Pending!=true)CompleteTurn();
        }
        public IEnumerable<int[]> CityArtifactSelections()
        {
            if(!Game.IsEndingTurn||_purchasedCityArtifacts==0||_cityArtifactChoices.Count==0)yield break;
            foreach(var choice in Select(new List<int>()))yield return choice;
            IEnumerable<int[]> Select(List<int> chosen)
            {
                if(chosen.Count==_purchasedCityArtifacts){yield return chosen.ToArray();yield break;}
                for(int i=0;i<_cityArtifactChoices.Count;i++)if(!chosen.Contains(i))
                {
                    chosen.Add(i);foreach(var c in Select(chosen))yield return c;chosen.RemoveAt(chosen.Count-1);
                }
            }
        }
    }
}
