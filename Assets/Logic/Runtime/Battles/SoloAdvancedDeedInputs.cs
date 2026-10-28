using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public static partial class SoloDeedInputs
    {
        /// <summary>Advanced actions except copied-effect chains, which need their own full continuation contract.</summary>
        public static IEnumerable<SoloDeedInput> AdvancedChoices(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int hand=0;hand<j.Player.Deck.Hand.Count;hand++)
            {
                var card=j.Player.Deck.Hand[hand];
                if(!card.Id.StartsWith("advanced_card_",StringComparison.Ordinal))continue;
                foreach(var choice in j.Context.InBattle?j.Battle.Choices(card):j.OutsideChoices(card))
                {
                    bool strong=choice.Enhanced||j.Context.NextCardEnhanced;
                    if(card.Id=="advanced_card_024")
                    {
                        for(int donor=0;donor<j.Player.Deck.Hand.Count;donor++)
                        {
                            var source=j.Player.Deck.Hand[donor];
                            if(donor==hand||source.Type!=CardType.Action)continue;
                            foreach(var borrowed in CopiedChoices(j,source.Id,strong,hand))
                            {
                                var copied=new SoloDeedInput(hand,choice.Id,Inputs:new(HandSlots:new[]{donor},
                                    BorrowedChoice:borrowed.Choice,BorrowedInputs:borrowed.Inputs),
                                    TargetHex:borrowed.TargetHex,TargetTerrain:borrowed.TargetTerrain);
                                bool legal=false;
                                try {Apply(episode,copied);legal=true;}
                                catch(InvalidOperationException) { }
                                finally {boundary.Restore();}
                                if(legal)yield return copied;
                            }
                        }
                        continue;
                    }
                    if(card.Id=="advanced_card_030"&&strong)
                    {
                        foreach(var copied in BloodlineChoices(j,hand,choice))
                        {
                            bool legal=false;
                            try {Apply(episode,copied);legal=true;}
                            catch(InvalidOperationException) { }
                            finally {boundary.Restore();}
                            if(legal)yield return copied;
                        }
                        continue;
                    }
                    var targets=choice.Target==BattleCardTarget.None?new[]{-1}
                        :Enumerable.Range(0,j.Battle?.Enemies.Count??0).ToArray();
                    foreach(var selection in AdvancedParameters(j,hand,card.Id,choice,strong))
                    foreach(int target in targets)
                    foreach(int attack in choice.Target==BattleCardTarget.EnemyAttack
                        ?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                    foreach(var hex in card.Id=="advanced_card_032"
                        ?j.Game.Map.Placed.Keys.OrderBy(p=>p.Q).ThenBy(p=>p.R).Select(p=>(AxialCoord?)p):new AxialCoord?[]{null})
                    {
                        var input=new SoloDeedInput(hand,choice.Id,target,attack,selection,hex);bool valid=false;
                        try {Apply(episode,input);valid=true;}
                        catch(InvalidOperationException) { }
                        finally {boundary.Restore();}
                        if(valid)yield return input;
                    }
                }
            }
        }
        internal static IEnumerable<SoloRepeatedInput> CopiedChoices(PveJourneySession j,string id,bool strong,int host=-1)
        {
            if(id is "advanced_card_024" or "advanced_card_030")yield break;
            foreach(var mode in PveCopyInputs.Choices(j.Context,id,strong))
            {
                IEnumerable<SoloDeedSelection> Parameters()
                {
                    if(id.StartsWith("advanced_card_",StringComparison.Ordinal))
                    {
                        foreach(var value in AdvancedParameters(j,host,id,mode,strong))yield return value;
                        yield break;
                    }
                    foreach(var colors in ColorSequences(PveDeedInputs.ColorCount(id,strong),new List<ManaColor>()))
                    foreach(var dice in DieSequences(j.Context.ManaSource?.Dice.Count??0,
                        id is "basic_card_009" or "basic_card_013"?PveDeedInputs.ColorCount(id,strong):0,new List<int>()))
                    foreach(int material in id=="basic_card_023"
                        ?Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=host):new[]{-1})
                        yield return new(Colors:colors,DiceSlots:dice,HandSlots:material<0?Array.Empty<int>():new[]{material});
                }
                foreach(var input in Parameters())
                foreach(int target in mode.Target==BattleCardTarget.None?new[]{-1}:Enumerable.Range(0,j.Battle?.Enemies.Count??0))
                foreach(int attack in mode.Target==BattleCardTarget.EnemyAttack?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                foreach(var spatial in BasicTargets(j,id,strong,mode.Option))
                foreach(var hex in id=="advanced_card_032"
                    ?j.Game.Map.Placed.Keys.OrderBy(p=>p.Q).ThenBy(p=>p.R).Select(p=>(AxialCoord?)p):new[]{spatial.hex})
                    yield return new(mode.Id,input with {TargetIndex=target,AttackIndex=attack,
                        UnitSlot=spatial.unit<0?input.UnitSlot:spatial.unit},hex,spatial.terrain);
            }
        }
        private static IEnumerable<SoloDeedInput> BloodlineChoices(PveJourneySession j,int hand,BattleCardChoice host)
        {
            var offer=j.Context.AdvActionSupply?.Offer;
            for(int slot=0;slot<(offer?.Count??0);slot++)
            {
                string id=offer[slot].Id;
                // Native PveCopyInputs rejects recursive copies. Do not fabricate them.
                if(id is "advanced_card_024" or "advanced_card_030")continue;
                foreach(var donor in PveCopyInputs.Choices(j.Context,id,true))
                foreach(var input in AdvancedParameters(j,hand,id,donor,true))
                foreach(int target in donor.Target==BattleCardTarget.None?new[]{-1}
                    :Enumerable.Range(0,j.Battle?.Enemies.Count??0))
                foreach(int attack in donor.Target==BattleCardTarget.EnemyAttack
                    ?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                foreach(var hex in id=="advanced_card_032"
                    ?j.Game.Map.Placed.Keys.OrderBy(p=>p.Q).ThenBy(p=>p.R).Select(p=>(AxialCoord?)p):new AxialCoord?[]{null})
                foreach(bool discard in new[]{false,true})
                    yield return new(hand,host.Id,Inputs:new(AdvancedSlot:slot,
                        BorrowedChoice:donor.Id,BorrowedInputs:input with {TargetIndex=target,AttackIndex=attack},
                        WoundToDiscard:discard),TargetHex:hex);
            }
        }
        private static IEnumerable<SoloDeedSelection> AdvancedParameters(PveJourneySession j,int hand,string id,BattleCardChoice choice,bool strong)
        {
            var others=Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=hand).ToArray();
            switch(id)
            {
                case "advanced_card_020":case "advanced_card_029":
                    foreach(int m in others)yield return new(HandSlots:new[]{m});yield break;
                case "advanced_card_027":
                    foreach(int m in others)for(int a=0;a<(j.Context.AdvActionSupply?.Offer.Count??0);a++)
                        yield return new(HandSlots:new[]{m},AdvancedSlot:a);yield break;
                case "advanced_card_025":
                    if(strong){for(int s=0;s<(j.Context.SpellSupply?.Offer.Count??0);s++)yield return new(SpellSlots:new[]{s});}
                    else foreach(int m in others)yield return new(HandSlots:new[]{m});yield break;
                case "advanced_card_030":
                    for(int a=0;a<(j.Context.AdvActionSupply?.Offer.Count??0);a++)yield return new(AdvancedSlot:a);yield break;
                case "advanced_card_033":
                    foreach(var s in DieSequences(j.Context.SpellSupply?.Offer.Count??0,strong?2:1,new List<int>()))
                        yield return new(SpellSlots:s);yield break;
                case "advanced_card_022" when !strong:
                    for(int d=0;d<(j.Context.ManaSource?.Dice.Count??0);d++)yield return new(DiceSlots:new[]{d});yield break;
                case "advanced_card_037":
                    var wounds=others.Where(i=>j.Player.Deck.Hand[i].Type==CardType.Wound).ToArray();
                    foreach(var selected in MaterialSubsets(wounds,0,new List<int>()))
                        if(strong || selected.Length<=1)yield return new(HandSlots:selected);yield break;
                case "advanced_card_038":
                    for(int u=0;u<j.Player.Units.Count;u++)yield return new(UnitSlot:u);yield break;
                case "advanced_card_040" when strong:
                    int sets;
                    try {sets=PveDeedInputs.ManaPowerSetsAfterPayment(j.Player,j.Context,choice);}
                    catch(InvalidOperationException){yield break;}
                    foreach(var bonuses in BonusSequences(sets,new List<int>()))yield return new(BonusChoices:bonuses);yield break;
            }
            foreach(var colors in ColorSequences(PveDeedInputs.ColorCount(id,strong),new List<ManaColor>()))
            {
                yield return new(Colors:colors);
                if(id=="advanced_card_012"&&strong)
                    foreach(var crystal in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})
                        yield return new(Colors:colors,Crystal:crystal);
            }
        }
        private static IEnumerable<int[]> MaterialSubsets(int[] slots,int at,List<int> prefix)
        {
            if(at==slots.Length){yield return prefix.ToArray();yield break;}
            foreach(var x in MaterialSubsets(slots,at+1,prefix))yield return x;
            prefix.Add(slots[at]);foreach(var x in MaterialSubsets(slots,at+1,prefix))yield return x;
            prefix.RemoveAt(prefix.Count-1);
        }
        private static IEnumerable<int[]> BonusSequences(int count,List<int> prefix)
        {
            if(count==0){yield return prefix.ToArray();yield break;}
            for(int i=0;i<3;i++)
            {prefix.Add(i);foreach(var x in BonusSequences(count-1,prefix))yield return x;prefix.RemoveAt(prefix.Count-1);}
        }
    }
}
