using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloTacticAbilityKind { MidnightMeditation, StorePower, CollectPower, LongNight, RightMoment, ReservedMana, ManaSearch }
    public sealed record SoloTacticAbility(SoloTacticAbilityKind Kind,int[] Slots);

    public static class SoloTacticAbilities
    {
        public static SoloTacticAbility[] Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || j.Game.HasPendingTacticSelection || j.SoloClock.DummyDue || j.Player.Tactic==null)
                return Array.Empty<SoloTacticAbility>();
            var proposals=new List<SoloTacticAbility>();
            switch(j.Player.Tactic.Card.Id)
            {
                case "night_4":proposals.AddRange(Subsets(j.Player.Deck.Hand.Count,5).Select(s=>new SoloTacticAbility(SoloTacticAbilityKind.MidnightMeditation,s)));break;
                case "night_6":
                    proposals.Add(new(SoloTacticAbilityKind.StorePower,Array.Empty<int>()));
                    proposals.Add(new(SoloTacticAbilityKind.CollectPower,Array.Empty<int>()));break;
                case "night_2":proposals.Add(new(SoloTacticAbilityKind.LongNight,Array.Empty<int>()));break;
                case "day_6":proposals.Add(new(SoloTacticAbilityKind.RightMoment,Array.Empty<int>()));break;
                case "day_3":proposals.Add(new(SoloTacticAbilityKind.ReservedMana,Array.Empty<int>()));break;
                case "night_3":proposals.AddRange(Subsets(j.Game.Globals.Mana.Dice.Count,2).Select(s=>new SoloTacticAbility(SoloTacticAbilityKind.ManaSearch,s)));break;
            }
            var checkpoint=DemoStateCheckpoint.Capture(episode);var legal=new List<SoloTacticAbility>();
            foreach(var input in proposals)
            {
                try { Apply(episode,input);legal.Add(input); }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
            }
            return legal.ToArray();
        }

        private static IEnumerable<int[]> Subsets(int count,int max,int start=0,List<int> chosen=null)
        {
            chosen??=new List<int>();yield return chosen.ToArray();
            if(chosen.Count==max)yield break;
            for(int i=start;i<count;i++)
            {
                chosen.Add(i);
                foreach(var subset in Subsets(count,max,i+1,chosen))yield return subset;
                chosen.RemoveAt(chosen.Count-1);
            }
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloTacticAbility input)
        {
            if(episode==null || input==null || input.Slots==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                if(j.SoloClock.DummyDue)throw new InvalidOperationException("Dummy must act before player tactic ability.");
                if(input.Slots.Distinct().Count()!=input.Slots.Length || input.Slots.Any(i=>i<0))
                    throw new InvalidOperationException("Invalid duplicate or negative tactic slots.");
                if(input.Kind!=SoloTacticAbilityKind.MidnightMeditation && input.Kind!=SoloTacticAbilityKind.ManaSearch && input.Slots.Length!=0)
                    throw new InvalidOperationException("This tactic has no slot parameters.");
                switch(input.Kind)
                {
                    case SoloTacticAbilityKind.MidnightMeditation:
                        if(input.Slots.Any(i=>i>=j.Player.Deck.Hand.Count))throw new InvalidOperationException("Invalid actual hand slot.");
                        j.Game.UseMidnightMeditation(input.Slots.Select(i=>j.Player.Deck.Hand[i]).ToArray());break;
                    case SoloTacticAbilityKind.ManaSearch:
                        if(input.Slots.Any(i=>i>=j.Game.Globals.Mana.Dice.Count))throw new InvalidOperationException("Invalid actual die slot.");
                        j.Game.UseManaSearch(input.Slots.Select(i=>j.Game.Globals.Mana.Dice[i]).ToArray());break;
                    case SoloTacticAbilityKind.StorePower:j.Game.ResolveSparingPower(false);break;
                    case SoloTacticAbilityKind.CollectPower:j.Game.ResolveSparingPower(true);break;
                    case SoloTacticAbilityKind.LongNight:j.Game.UseLongNight(j.Player);break;
                    case SoloTacticAbilityKind.RightMoment:j.Game.UseRightMoment();break;
                    case SoloTacticAbilityKind.ReservedMana:j.Game.UseReservedTacticMana();break;
                    default:throw new ArgumentOutOfRangeException(nameof(input.Kind));
                }
            });
        }
    }
}
