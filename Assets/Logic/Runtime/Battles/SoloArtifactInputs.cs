using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloArtifactInput(int HandSlot,bool Burn,int UnitSlot=-1,ManaColor[] Colors=null,
        int[] MaterialSlots=null,int[] EnemySlots=null,int HornBonus=0,bool ShieldSplit=false,
        int OfferedUnitSlot=-1,int DisbandUnitSlot=-1);
    public static class SoloArtifactInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloArtifactInput input)
        {
            if(episode==null||input==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                if(input.HandSlot<0||input.HandSlot>=j.Player.Deck.Hand.Count)
                    throw new InvalidOperationException("Invalid actual artifact hand slot.");
                var card=j.Player.Deck.Hand[input.HandSlot];
                if(card.Type!=CardType.Artifact)throw new InvalidOperationException("Actual artifact required.");
                if(card.Id=="items_017")
                {
                    if(input.UnitSlot!=-1||input.Colors?.Length>0||input.MaterialSlots?.Length>0||input.EnemySlots?.Length>0
                        ||input.HornBonus!=0||input.ShieldSplit||input.DisbandUnitSlot < -1
                        ||input.DisbandUnitSlot>=j.Player.Units.Count
                        ||(input.Burn?(input.OfferedUnitSlot<0||input.OfferedUnitSlot>=j.Game.Globals.Units.Offer.Count)
                            :input.OfferedUnitSlot!=-1||input.DisbandUnitSlot!=-1))
                        throw new InvalidOperationException("Invalid command banner mode or actual unit selection.");
                    j.PlaySpecialArtifact(card,input.Burn,
                        offer:input.Burn?j.Game.Globals.Units.Offer[input.OfferedUnitSlot]:null,
                        disband:input.DisbandUnitSlot<0?null:j.Player.Units[input.DisbandUnitSlot]);
                    return;
                }
                if(input.OfferedUnitSlot!=-1||input.DisbandUnitSlot!=-1)
                    throw new InvalidOperationException("Recruitment parameters require command banner.");
                bool resource=PveArtifactInputs.IsResource(card.Id);
                bool attached=PveArtifactInputs.IsAttachedBanner(card.Id)&&!input.Burn;
                if(!resource&&!PveArtifactInputs.IsAttachedBanner(card.Id)
                    &&card.Id is not ("items_008" or "items_009" or "items_012" or "items_016" or "items_019" or "items_020"))
                    throw new InvalidOperationException("This artifact requires its specialized parameter branch.");
                if(attached?(input.UnitSlot<0||input.UnitSlot>=j.Player.Units.Count):input.UnitSlot!=-1)
                    throw new InvalidOperationException("Artifact unit slot does not match the actual mode.");
                var colors=input.Colors??Array.Empty<ManaColor>();
                if(!resource&&colors.Length!=0)throw new InvalidOperationException("Extraneous artifact colors.");
                var unit=attached?j.Player.Units[input.UnitSlot]:null;
                var materials=input.MaterialSlots??Array.Empty<int>();var enemies=input.EnemySlots??Array.Empty<int>();
                if(materials.Distinct().Count()!=materials.Length||materials.Any(i=>i<0||i>=j.Player.Deck.Hand.Count||i==input.HandSlot)
                    ||materials.Length>0&&(input.Burn||card.Id is not ("items_008" or "items_016")))
                    throw new InvalidOperationException("Invalid actual artifact material slots.");
                if(enemies.Length>0&&!(card.Id=="items_001"&&input.Burn)
                    ||input.HornBonus!=0&&card.Id!="items_009"||input.ShieldSplit&&card.Id!="items_020")
                    throw new InvalidOperationException("Extraneous combat artifact parameters.");
                if(j.Context.InBattle)
                {
                    if(resource)j.Battle.PlayResourceArtifact(card,input.Burn,colors);
                    else if(card.Id=="items_009")j.Battle.PlayHorn(card,input.Burn,input.HornBonus);
                    else if(card.Id=="items_020")j.Battle.PlayShield(card,input.Burn,input.ShieldSplit);
                    else if(card.Id=="items_001"&&input.Burn)j.Battle.BurnFearBanner(card,enemies);
                    else j.Battle.PlayArtifact(card,input.Burn,unit,materials.Select(i=>j.Player.Deck.Hand[i]).ToArray());
                }
                else j.PlayOutsideArtifact(card,input.Burn,unit,colors);
            });
        }
        public static IEnumerable<SoloArtifactInput> Legal(SoloConquestEpisode episode)
        {
            var j=episode.Journey;
            if(episode.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int hand=0;hand<j.Player.Deck.Hand.Count;hand++)
            {
                var card=j.Player.Deck.Hand[hand];
                if(card.Type!=CardType.Artifact)continue;
                if(card.Id=="items_017")
                {
                    foreach(bool burn in new[]{false,true})
                    foreach(int offer in burn?Enumerable.Range(0,j.Game.Globals.Units.Offer.Count):new[]{-1})
                    foreach(int disband in burn?Enumerable.Range(-1,j.Player.Units.Count+1):new[]{-1})
                    {
                        var input=new SoloArtifactInput(hand,burn,OfferedUnitSlot:offer,DisbandUnitSlot:disband);bool valid=false;
                        try{Apply(episode,input);valid=true;}catch(InvalidOperationException){}
                        finally{boundary.Restore();}
                        if(valid)yield return input;
                    }
                    continue;
                }
                bool resource=PveArtifactInputs.IsResource(card.Id),banner=PveArtifactInputs.IsAttachedBanner(card.Id);
                if(!resource&&!banner&&card.Id is not ("items_008" or "items_009" or "items_012" or "items_016" or "items_019" or "items_020"))continue;
                foreach(bool burn in new[]{false,true})
                {
                    if(!j.Context.InBattle&&!j.CanPlayOutsideArtifact(card,burn))continue;
                    foreach(int unit in banner&&!burn?Enumerable.Range(0,j.Player.Units.Count):new[]{-1})
                    foreach(var colors in Colors(PveArtifactInputs.ColorCount(card.Id,burn),card.Id=="items_011"?4:6))
                    foreach(var materials in !burn&&card.Id is "items_008" or "items_016"
                        ?Subsets(Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=hand&&j.Player.Deck.Hand[i].Type!=CardType.Wound).ToArray(),0,new List<int>(),int.MaxValue)
                        :new[]{Array.Empty<int>()})
                    foreach(var enemies in burn&&card.Id=="items_001"
                        ?Subsets(Enumerable.Range(0,j.Battle.Enemies.Count).ToArray(),0,new List<int>(),3):new[]{Array.Empty<int>()})
                    foreach(int bonus in card.Id=="items_009"&&burn?Enumerable.Range(0,6):new[]{0})
                    foreach(bool split in card.Id=="items_020"?new[]{false,true}:new[]{false})
                    {
                        var input=new SoloArtifactInput(hand,burn,unit,colors,materials,enemies,bonus,split);bool valid=false;
                        try{Apply(episode,input);valid=true;}
                        catch(InvalidOperationException){}
                        finally{boundary.Restore();}
                        if(valid)yield return input;
                    }
                }
            }
        }
        private static IEnumerable<int[]> Subsets(int[] slots,int at,List<int> chosen,int limit)
        {
            if(at==slots.Length){yield return chosen.ToArray();yield break;}
            foreach(var row in Subsets(slots,at+1,chosen,limit))yield return row;
            if(chosen.Count>=limit)yield break;
            chosen.Add(slots[at]);
            foreach(var row in Subsets(slots,at+1,chosen,limit))yield return row;
            chosen.RemoveAt(chosen.Count-1);
        }
        private static IEnumerable<ManaColor[]> Colors(int count,int options)
        {
            if(count==0){yield return Array.Empty<ManaColor>();yield break;}
            foreach(var tail in Colors(count-1,options))
            for(int i=0;i<options;i++)yield return new[]{(ManaColor)i}.Concat(tail).ToArray();
        }
    }
}
