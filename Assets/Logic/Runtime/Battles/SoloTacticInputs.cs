using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloTacticInputKind { Rethink, ReserveDie, SearchDeck }
    public sealed record SoloTacticInput(SoloTacticInputKind Kind, int[] HandSlots, int DieSlot = -1, string CardId = null);

    /// <summary>Original acquisition-time tactic choices. Deck search exposes card identities
    /// in canonical order, never their hidden draw positions. It shuffles via the native rule.</summary>
    public static class SoloTacticInputs
    {
        public static SoloTacticInput[] Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var journey=episode.Journey;var p=journey.Player;
            if(episode.Terminal || !journey.Game.HasPendingTacticSelection || journey.Game.HasActiveTurn)
                return Array.Empty<SoloTacticInput>();
            var candidates=new List<SoloTacticInput>();
            switch(p.Tactic.Card.Id)
            {
                case "day_2":
                    candidates.Add(new(SoloTacticInputKind.Rethink,Array.Empty<int>()));
                    for(int a=0;a<p.Deck.Hand.Count;a++)
                    {
                        candidates.Add(new(SoloTacticInputKind.Rethink,new[]{a}));
                        for(int b=a+1;b<p.Deck.Hand.Count;b++)
                        {
                            candidates.Add(new(SoloTacticInputKind.Rethink,new[]{a,b}));
                            for(int c=b+1;c<p.Deck.Hand.Count;c++)
                                candidates.Add(new(SoloTacticInputKind.Rethink,new[]{a,b,c}));
                        }
                    }
                    break;
                case "day_3":
                    for(int i=0;i<journey.Game.Globals.Mana.Dice.Count;i++)
                        candidates.Add(new(SoloTacticInputKind.ReserveDie,Array.Empty<int>(),i));
                    break;
                case "night_5":
                    foreach(var id in p.Deck.DrawPile.Select(c=>c.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal))
                        candidates.Add(new(SoloTacticInputKind.SearchDeck,Array.Empty<int>(),CardId:id));
                    break;
                default:throw new InvalidOperationException("Unimplemented pending original tactic input.");
            }
            var checkpoint=DemoStateCheckpoint.Capture(episode);var legal=new List<SoloTacticInput>();
            foreach(var choice in candidates)
            {
                try { Apply(episode,choice);legal.Add(choice); }
                catch(InvalidOperationException) { }
                finally { checkpoint.Restore(); }
            }
            return legal.ToArray();
        }

        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloTacticInput input)
        {
            if(episode==null || input==null || input.HandSlots==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                if(!j.Game.HasPendingTacticSelection)throw new InvalidOperationException("No pending tactic choice.");
                var p=j.Player;
                switch(input.Kind)
                {
                    case SoloTacticInputKind.Rethink:
                        if(p.Tactic.Card.Id!="day_2" || input.DieSlot!=-1 || input.CardId!=null
                            || input.HandSlots.Length>3 || input.HandSlots.Distinct().Count()!=input.HandSlots.Length
                            || input.HandSlots.Any(i=>i<0 || i>=p.Deck.Hand.Count))throw new InvalidOperationException("Invalid rethink parameters.");
                        j.Game.ResolveTacticSelection(p,input.HandSlots.Select(i=>p.Deck.Hand[i]).ToArray());break;
                    case SoloTacticInputKind.ReserveDie:
                        if(p.Tactic.Card.Id!="day_3" || input.HandSlots.Length!=0 || input.CardId!=null
                            || input.DieSlot<0 || input.DieSlot>=j.Game.Globals.Mana.Dice.Count)throw new InvalidOperationException("Invalid source die.");
                        j.Game.ResolveTacticSelection(p,die:j.Game.Globals.Mana.Dice[input.DieSlot]);break;
                    case SoloTacticInputKind.SearchDeck:
                        if(p.Tactic.Card.Id!="night_5" || input.HandSlots.Length!=0 || input.DieSlot!=-1
                            || input.CardId==null)throw new InvalidOperationException("Invalid searched card.");
                        var card=p.Deck.DrawPile.FirstOrDefault(c=>c.Id==input.CardId)
                            ??throw new InvalidOperationException("Card absent from actual draw pile.");
                        j.Game.ResolveTacticSelection(p,new[]{card});break;
                    default:throw new ArgumentOutOfRangeException(nameof(input.Kind));
                }
            });
        }
    }
}
