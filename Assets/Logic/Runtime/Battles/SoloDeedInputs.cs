using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Serializable physical references for native deed inputs. Slots are local to
    /// the saved decision boundary. No hidden draw-pile indices are accepted.</summary>
    public sealed record SoloDeedSelection(int[] DiceSlots=null,ManaColor[] Colors=null,
        int[] HandSlots=null,ManaColor? Crystal=null,int AdvancedSlot=-1,int[] SpellSlots=null,
        int UnitSlot=-1,int[] UnitSlots=null,int[] BonusChoices=null,int OfferedUnitSlot=-1,
        int DisbandUnitSlot=-1,string BorrowedChoice=null,SoloDeedSelection BorrowedInputs=null,
        bool WoundToDiscard=false,int TargetIndex=-1,int AttackIndex=0,
        AxialCoord[] TravelPath=null,int[] EnemyIndices=null,int[] DiscardSlots=null)
    {
        private static T At<T>(IReadOnlyList<T> items,int index,string name)
        {
            if(items==null || index<0 || index>=items.Count)throw new InvalidOperationException("Invalid actual "+name+" slot.");
            return items[index];
        }
        public PveDeedSelection Resolve(PveJourneySession j)
        {
            var p=j.Player;var c=j.Context;
            if(UnitSlot < -1 || AdvancedSlot < -1 || OfferedUnitSlot < -1 || DisbandUnitSlot < -1)
                throw new InvalidOperationException("Optional physical slots must use -1 for absence.");
            if(HandSlots?.Length>0 && DiscardSlots?.Length>0)
                throw new InvalidOperationException("A deed material selection must identify one physical zone.");
            return new PveDeedSelection
            {
                Dice=(DiceSlots??Array.Empty<int>()).Select(i=>At(c.ManaSource?.Dice.ToArray(),i,"source die")).ToArray(),
                Colors=(Colors??Array.Empty<ManaColor>()).ToArray(),
                Cards=DiscardSlots!=null?DiscardSlots.Select(i=>At(p.Deck.DiscardPile,i,"discard material")).ToArray()
                    :(HandSlots??Array.Empty<int>()).Select(i=>At(p.Deck.Hand,i,"hand material")).ToArray(),
                Crystal=Crystal,Advanced=AdvancedSlot<0?null:At(c.AdvActionSupply?.Offer,AdvancedSlot,"advanced offer"),
                Spells=(SpellSlots??Array.Empty<int>()).Select(i=>At(c.SpellSupply?.Offer,i,"spell offer")).ToArray(),
                Unit=UnitSlot<0?null:At(p.Units,UnitSlot,"unit"),
                Units=UnitSlots?.Select(i=>At(p.Units,i,"unit")).ToArray(),
                BonusChoices=(BonusChoices??Array.Empty<int>()).ToArray(),
                OfferedUnit=OfferedUnitSlot<0?null:At(j.Game.Globals.Units.Offer,OfferedUnitSlot,"unit offer"),
                DisbandUnit=DisbandUnitSlot<0?null:At(p.Units,DisbandUnitSlot,"dismiss unit"),
                BorrowedChoice=BorrowedChoice,BorrowedInputs=BorrowedInputs?.Resolve(j),
                WoundToDiscard=WoundToDiscard,TargetIndex=TargetIndex,AttackIndex=AttackIndex,
                TravelPath=(TravelPath??Array.Empty<AxialCoord>()).ToArray(),
                EnemyIndices=(EnemyIndices??Array.Empty<int>()).ToArray()
            };
        }
    }

    public sealed record SoloDeedInput(int HandSlot,string Choice,int EnemySlot=-1,int AttackIndex=0,
        SoloDeedSelection Inputs=null,AxialCoord? TargetHex=null,TerrainType? TargetTerrain=null);

    public static partial class SoloDeedInputs
    {
        /// <summary>Original basic-deed branch, including source-die transforms and physical
        /// improvisation materials. Acquired advanced/spell/artifact generators are separate work.</summary>
        public static IEnumerable<SoloDeedInput> BasicChoices(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int hand=0;hand<j.Player.Deck.Hand.Count;hand++)
            {
                var card=j.Player.Deck.Hand[hand];
                if(!card.Id.StartsWith("basic_card_",StringComparison.Ordinal))continue;
                var choices=j.Context.InBattle?j.Battle.Choices(card):j.OutsideChoices(card);
                foreach(var choice in choices)
                {
                    bool strong=choice.Enhanced||j.Context.NextCardEnhanced;
                    int colorCount=PveDeedInputs.ColorCount(card.Id,strong);
                    var materialSlots=card.Id=="basic_card_023"
                        ?Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=hand).ToArray():new[]{-1};
                    foreach(var colors in ColorSequences(colorCount,new List<ManaColor>()))
                    foreach(var dice in DieSequences(j.Context.ManaSource?.Dice.Count??0,
                        card.Id is "basic_card_009" or "basic_card_013"?colorCount:0,new List<int>()))
                    foreach(int material in materialSlots)
                    foreach(var target in BasicTargets(j,card.Id,strong,choice.Option))
                    {
                        var selection=new SoloDeedSelection(DiceSlots:dice,Colors:colors,
                            HandSlots:material<0?Array.Empty<int>():new[]{material},UnitSlot:target.unit);
                        var input=new SoloDeedInput(hand,choice.Id,Inputs:selection,TargetHex:target.hex,TargetTerrain:target.terrain);bool valid=false;
                        try {Apply(episode,input);valid=true;}
                        catch(InvalidOperationException) { }
                        finally {boundary.Restore();}
                        if(valid)yield return input;
                    }
                }
            }
        }
        private static IEnumerable<(AxialCoord? hex,TerrainType? terrain,int unit)> BasicTargets(PveJourneySession j,string id,bool strong,int option)
        {
            if(id=="basic_card_018")
            {
                if(strong)foreach(TerrainType terrain in Enum.GetValues(typeof(TerrainType)))yield return (null,terrain,-1);
                else foreach(var hex in j.Game.Map.Placed.Keys.OrderBy(p=>p.Q).ThenBy(p=>p.R))yield return (hex,null,-1);
            }
            else if(id=="basic_card_005" && option==3)
            {
                for(int unit=0;unit<j.Player.Units.Count;unit++)yield return (null,null,unit);
            }
            else yield return (null,null,-1);
        }
        private static IEnumerable<ManaColor[]> ColorSequences(int count,List<ManaColor> prefix)
        {
            if(count==0){yield return prefix.ToArray();yield break;}
            foreach(ManaColor color in Enum.GetValues(typeof(ManaColor)))
            {
                prefix.Add(color);
                foreach(var values in ColorSequences(count-1,prefix))yield return values;
                prefix.RemoveAt(prefix.Count-1);
            }
        }
        private static IEnumerable<int[]> DieSequences(int total,int count,List<int> prefix)
        {
            if(count==0){yield return prefix.ToArray();yield break;}
            for(int i=0;i<total;i++)
            {
                if(prefix.Contains(i))continue;
                prefix.Add(i);
                foreach(var values in DieSequences(total,count-1,prefix))yield return values;
                prefix.RemoveAt(prefix.Count-1);
            }
        }
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloDeedInput input)
        {
            if(episode==null || input==null)throw new ArgumentNullException();
            return episode.Apply(j=>Execute(j,input));
        }
        public static void Execute(PveJourneySession j,SoloDeedInput input)
        {
            if(input.HandSlot<0 || input.HandSlot>=j.Player.Deck.Hand.Count || string.IsNullOrEmpty(input.Choice))
                throw new InvalidOperationException("Choose an actual hand card and its native mode.");
            var card=j.Player.Deck.Hand[input.HandSlot];
            var selection=input.Inputs?.Resolve(j);
            var oldHex=j.Context.TargetHex;var oldTerrain=j.Context.TargetTerrain;
            try
            {
                j.Context.TargetHex=input.TargetHex;j.Context.TargetTerrain=input.TargetTerrain;
                if(j.Context.InBattle)
                {
                    if(j.Battle==null)throw new InvalidOperationException("Missing actual battle controller.");
                    j.Battle.Play(card,input.Choice,input.EnemySlot,input.AttackIndex,selection);
                }
                else
                {
                    if(input.EnemySlot!=-1 || input.AttackIndex!=0)throw new InvalidOperationException("Outside deed cannot select a combat attack.");
                    if(selection?.Unit!=null && !PveDeedInputs.Supports(card.Id))
                        j.PlayOutsideBattle(card,input.Choice,selection.Unit);
                    else j.PlayOutsideWithInputs(card,input.Choice,selection);
                }
            }
            finally {j.Context.TargetHex=oldHex;j.Context.TargetTerrain=oldTerrain;}
        }
    }
}
