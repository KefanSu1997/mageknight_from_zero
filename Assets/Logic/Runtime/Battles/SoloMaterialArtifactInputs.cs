using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloMaterialArtifactInput(int HandSlot,bool Burn,int MaterialSlot=-1,int OfferSlot=-1,
        int First=0,int Second=0,ManaColor Crystal=ManaColor.Red,int UnitSlot=-1,AxialCoord[] Path=null,
        string SpellChoice=null,int EnemySlot=-1,SoloDeedSelection SpellInputs=null);
    public static class SoloMaterialArtifactInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloMaterialArtifactInput input)
        {
            return episode.Apply(j=>
            {
                var p=j.Player;
                if(input.HandSlot<0||input.HandSlot>=p.Deck.Hand.Count)throw new InvalidOperationException("Invalid artifact hand slot.");
                var card=p.Deck.Hand[input.HandSlot];
                if(card.Type!=CardType.Artifact||card.Id is not ("items_013" or "items_021" or "items_023"))
                    throw new InvalidOperationException("Expected actual material artifact.");
                if(card.Id!="items_023"&&(input.SpellChoice!=null||input.EnemySlot!=-1||input.SpellInputs!=null))
                    throw new InvalidOperationException("Borrowed spell parameters require the tome.");
                bool material=card.Id!="items_021"||!input.Burn;
                if(material?(input.MaterialSlot<0||input.MaterialSlot>=p.Deck.Hand.Count||input.MaterialSlot==input.HandSlot):input.MaterialSlot!=-1)
                    throw new InvalidOperationException("Invalid actual material slot.");
                var source=material?p.Deck.Hand[input.MaterialSlot]:null;
                if(card.Id=="items_023")
                {
                    if(input.First!=0||input.Second!=0||input.Crystal!=ManaColor.Red||input.UnitSlot!=-1||input.Path!=null
                        ||input.OfferSlot<0||input.OfferSlot>=j.Game.Globals.Spells.Offer.Count)
                        throw new InvalidOperationException("Invalid tome supply or extraneous staff parameters.");
                    j.PlayMaterialArtifact(card,input.Burn,source,spell:j.Game.Globals.Spells.Offer[input.OfferSlot],
                        spellChoice:input.SpellChoice,target:input.EnemySlot,inputs:(input.SpellInputs??new()).Resolve(j));
                }
                else if(card.Id=="items_013")
                {
                    if(input.First!=0||input.Second!=0||input.Crystal!=ManaColor.Red||input.UnitSlot!=-1||input.Path?.Length>0)
                        throw new InvalidOperationException("Extraneous book parameters.");
                    int count=input.Burn?j.Game.Globals.Spells.Offer.Count:j.Game.Globals.AdvActions.Offer.Count;
                    if(input.OfferSlot<0||input.OfferSlot>=count)throw new InvalidOperationException("Invalid actual book reward offer.");
                    j.PlayMaterialArtifact(card,input.Burn,source,
                        advanced:input.Burn?null:j.Game.Globals.AdvActions.Offer[input.OfferSlot],
                        spell:input.Burn?j.Game.Globals.Spells.Offer[input.OfferSlot]:null);
                }
                else
                {
                    bool unit=input.First==2||input.Burn&&input.Second==2;
                    bool travel=input.First==0||input.Burn&&input.Second==0;
                    bool crystal=input.First==1||input.Burn&&input.Second==1;
                    if(input.OfferSlot!=-1||!input.Burn&&input.Second!=0||!crystal&&input.Crystal!=ManaColor.Red
                        ||!travel&&input.Path?.Length>0||
                        (unit?(input.UnitSlot<0||input.UnitSlot>=p.Units.Count):input.UnitSlot!=-1))
                        throw new InvalidOperationException("Staff parameters do not match selected effects.");
                    j.PlayMaterialArtifact(card,input.Burn,source,unit:unit?p.Units[input.UnitSlot]:null,
                        first:input.First,second:input.Second,crystal:input.Crystal,path:input.Path??Array.Empty<AxialCoord>());
                }
            });
        }
        public static IEnumerable<SoloMaterialArtifactInput> Legal(SoloConquestEpisode episode)
        {
            var j=episode.Journey;
            if(episode.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int hand=0;hand<j.Player.Deck.Hand.Count;hand++)
            {
                var card=j.Player.Deck.Hand[hand];
                if(card.Type!=CardType.Artifact||card.Id is not ("items_013" or "items_021" or "items_023"))continue;
                foreach(bool burn in new[]{false,true})
                foreach(int material in card.Id!="items_021"||!burn
                    ?Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=hand):new[]{-1})
                foreach(var input in Candidates(j,hand,card.Id,burn,material))
                {
                    bool valid=false;
                    try{Apply(episode,input);valid=true;}catch(InvalidOperationException){}
                    finally{boundary.Restore();}
                    if(valid)yield return input;
                }
            }
        }
        private static IEnumerable<SoloMaterialArtifactInput> Candidates(PveJourneySession j,int hand,string id,bool burn,int material)
        {
            if(id=="items_023")
            {
                for(int offer=0;offer<j.Game.Globals.Spells.Offer.Count;offer++)
                {
                    var spell=j.Game.Globals.Spells.Offer[offer];
                    if(spell.Id is "magic_000" or "magic_006" or "magic_012" or "magic_018")continue;
                    foreach(var choice in j.TomeChoices(spell,burn))
                    foreach(var selection in SoloDeedInputs.SpellParameters(j,hand,spell.Id,burn))
                    {
                        if(selection.HandSlots?.Contains(material)==true)continue;
                        foreach(int target in choice.Target==BattleCardTarget.None?new[]{-1}:Enumerable.Range(0,j.Battle?.Enemies.Count??0))
                        foreach(int attack in choice.Target==BattleCardTarget.EnemyAttack
                            ?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                            yield return new(hand,burn,material,offer,SpellChoice:choice.Id,EnemySlot:target,
                                SpellInputs:selection with {TargetIndex=target,AttackIndex=attack});
                    }
                }
                yield break;
            }
            if(id=="items_013")
            {
                int n=burn?j.Game.Globals.Spells.Offer.Count:j.Game.Globals.AdvActions.Offer.Count;
                for(int i=0;i<n;i++)yield return new(hand,burn,material,i);
                yield break;
            }
            for(int first=0;first<4;first++)
            foreach(int second in burn?Enumerable.Range(0,4).Where(i=>i!=first):new[]{0})
            foreach(int unit in first==2||burn&&second==2?Enumerable.Range(0,j.Player.Units.Count):new[]{-1})
            foreach(var crystal in first==1||burn&&second==1?new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}:new[]{ManaColor.Red})
            foreach(var path in first==0||burn&&second==0?Paths(j,j.Player.Position,2):new[]{Array.Empty<AxialCoord>()})
                yield return new(hand,burn,material,First:first,Second:second,Crystal:crystal,UnitSlot:unit,Path:path);
        }
        private static IEnumerable<AxialCoord[]> Paths(PveJourneySession j,AxialCoord from,int remaining)
        {
            yield return Array.Empty<AxialCoord>();
            if(remaining==0)yield break;
            foreach(var offset in AxialCoord.NeighborDirs)
            {
                var next=from+offset;
                if(!j.Game.Map.Placed.ContainsKey(next))continue;
                foreach(var rest in Paths(j,next,remaining-1))yield return new[]{next}.Concat(rest).ToArray();
            }
        }
    }
}
