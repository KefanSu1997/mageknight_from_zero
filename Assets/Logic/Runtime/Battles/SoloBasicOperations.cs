using System;
using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloBasicKind { SourceDie, SidewaysMovement, SidewaysInfluence, SidewaysCombat, AdvanceCombat }
    public sealed record SoloBasicInput(SoloBasicKind Kind,int Slot=-1,ManaColor? Color=null,bool Extra=false,Phase? ExpectedPhase=null);
    public static class SoloBasicOperations
    {
        public static void Execute(PveJourneySession j,SoloBasicInput input)
        {
            if(j==null || input==null)throw new ArgumentNullException();
            if(!j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Game.HasPendingTacticSelection
                ||j.SoloClock?.ScenarioEnded==true || j.Context==null)
                throw new InvalidOperationException("Basic operation requires the actual active player turn.");
            if(input.Kind!=SoloBasicKind.SourceDie && (input.Color.HasValue||input.Extra))
                throw new InvalidOperationException("Mana parameters require source-die operation.");
            if(input.Kind!=SoloBasicKind.AdvanceCombat && input.ExpectedPhase.HasValue)
                throw new InvalidOperationException("Phase parameter requires phase advance.");
            switch(input.Kind)
            {
                case SoloBasicKind.SourceDie:
                    if(input.Slot<0 || input.Slot>=(j.Context.ManaSource?.Dice.Count??0) || !input.Color.HasValue)
                        throw new InvalidOperationException("Choose an actual public source die and color.");
                    CardManaActions.UseSourceDie(j.Player,j.Context,j.Context.ManaSource.Dice[input.Slot],input.Color.Value,input.Extra);break;
                case SoloBasicKind.AdvanceCombat:
                    if(input.Slot!=-1 || j.Battle==null || input.ExpectedPhase!=j.Battle.Phase
                        ||j.Battle.Phase is not (Phase.Ranged or Phase.Block or Phase.Melee))
                        throw new InvalidOperationException("Phase must match; damage requires a complete allocation command.");
                    j.Battle.Advance();break;
                case SoloBasicKind.SidewaysMovement:
                case SoloBasicKind.SidewaysInfluence:
                case SoloBasicKind.SidewaysCombat:
                    if(input.Slot<0 || input.Slot>=j.Player.Deck.Hand.Count)throw new InvalidOperationException("Invalid hand slot.");
                    var card=j.Player.Deck.Hand[input.Slot];
                    if(input.Kind==SoloBasicKind.SidewaysMovement)j.SidewaysMovement(card);
                    else if(input.Kind==SoloBasicKind.SidewaysInfluence)j.SidewaysInfluence(card);
                    else (j.Battle??throw new InvalidOperationException("No actual battle")).PlaySideways(card);
                    break;
                default:throw new InvalidOperationException("Unknown basic operation.");
            }
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloBasicInput input)=>e.Apply(j=>Execute(j,input));
        public static IEnumerable<SoloBasicInput> Legal(SoloConquestEpisode e)
        {
            if(e==null)throw new ArgumentNullException(nameof(e));
            var j=e.Journey;
            if(e.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null)yield break;
            var proposals=new List<SoloBasicInput>();
            for(int i=0;i<(j.Context.ManaSource?.Dice.Count??0);i++)
                foreach(ManaColor c in Enum.GetValues(typeof(ManaColor)))
                {proposals.Add(new(SoloBasicKind.SourceDie,i,c));proposals.Add(new(SoloBasicKind.SourceDie,i,c,true));}
            for(int i=0;i<j.Player.Deck.Hand.Count;i++)
                foreach(var kind in new[]{SoloBasicKind.SidewaysMovement,SoloBasicKind.SidewaysInfluence,SoloBasicKind.SidewaysCombat})
                    proposals.Add(new(kind,i));
            if(j.Battle!=null)proposals.Add(new(SoloBasicKind.AdvanceCombat,ExpectedPhase:j.Battle.Phase));
            var checkpoint=DemoStateCheckpoint.Capture(e);
            foreach(var input in proposals)
            {
                bool valid=false;
                try {Apply(e,input);valid=true;}
                catch(InvalidOperationException) { }
                finally {checkpoint.Restore();}
                if(valid)yield return input;
            }
        }
    }
}
