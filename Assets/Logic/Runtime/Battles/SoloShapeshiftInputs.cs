using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloShapeshiftInput(int SkillSlot,int HandSlot,bool Enhanced,int Option,
        FixedOutputKind Target,int MaterialSlot=-1,AxialCoord? Hex=null,TerrainType? Terrain=null);
    public static class SoloShapeshiftInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode e,SoloShapeshiftInput input)
        {
            if(e==null||input==null)throw new ArgumentNullException();
            return e.Apply(j=>
            {
                var p=j.Player;
                if(input.SkillSlot<0||input.SkillSlot>=p.Skills.Count||input.HandSlot<0||input.HandSlot>=p.Deck.Hand.Count
                    ||input.MaterialSlot < -1||input.MaterialSlot>=p.Deck.Hand.Count||input.MaterialSlot==input.HandSlot)
                    throw new InvalidOperationException("Invalid physical shapeshift selection.");
                var card=p.Deck.Hand[input.HandSlot];
                bool strong=input.Enhanced||j.Context.NextCardEnhanced;
                if(card.Id=="basic_card_023"?input.MaterialSlot<0:input.MaterialSlot!=-1)
                    throw new InvalidOperationException("Shapeshift must retain original material payment.");
                if(card.Id=="basic_card_018"
                    ?strong?input.Hex.HasValue||!input.Terrain.HasValue:!input.Hex.HasValue||input.Terrain.HasValue
                    :input.Hex.HasValue||input.Terrain.HasValue)
                    throw new InvalidOperationException("Shapeshift terrain parameters differ from original card mode.");
                var material=input.MaterialSlot<0?null:p.Deck.Hand[input.MaterialSlot];
                if(j.Context.InBattle)j.Battle.PlayShapeshift(p.Skills[input.SkillSlot],card,input.Enhanced,
                    input.Option,input.Target,material,input.Hex,input.Terrain);
                else j.PlayShapeshift(p.Skills[input.SkillSlot],card,input.Enhanced,
                    input.Option,input.Target,material,input.Hex,input.Terrain);
            });
        }
        public static IEnumerable<SoloShapeshiftInput> Legal(SoloConquestEpisode e)
        {
            var j=e.Journey;var p=j.Player;
            if(e.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn||j.Context==null)yield break;
            var boundary=DemoStateCheckpoint.Capture(e);
            for(int s=0;s<p.Skills.Count;s++)
            {
                if(p.Skills[s].SourceId!="hero_skill_pool_001:06"||SkillActions.IsUsed(p,p.Skills[s]))continue;
                for(int h=0;h<p.Deck.Hand.Count;h++)
                {
                    var card=p.Deck.Hand[h];
                    if(card.Type!=CardType.Action||!card.Id.StartsWith("basic_card_",StringComparison.Ordinal))continue;
                    foreach(bool enhanced in new[]{false,true})
                    {
                        bool strong=enhanced||j.Context.NextCardEnhanced;
                        foreach(var output in ShapeshiftActions.Choices(card,strong))
                        foreach(var target in Enum.GetValues(typeof(FixedOutputKind)).Cast<FixedOutputKind>().Where(t=>t!=output.Kind))
                        foreach(int material in card.Id=="basic_card_023"?Enumerable.Range(0,p.Deck.Hand.Count).Where(i=>i!=h):new[]{-1})
                        foreach(AxialCoord? hex in card.Id=="basic_card_018"&&!strong
                            ?j.Game.Map.Placed.Keys.OrderBy(x=>x.Q).ThenBy(x=>x.R).Select(x=>(AxialCoord?)x):new AxialCoord?[]{null})
                        foreach(TerrainType? terrain in card.Id=="basic_card_018"&&strong
                            ?Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().Select(x=>(TerrainType?)x):new TerrainType?[]{null})
                        {
                            var input=new SoloShapeshiftInput(s,h,enhanced,output.Option,target,material,hex,terrain);bool legal=false;
                            try{Apply(e,input);legal=true;}catch(InvalidOperationException){}
                            finally{boundary.Restore();}
                            if(legal)yield return input;
                        }
                    }
                }
            }
        }
    }
}
