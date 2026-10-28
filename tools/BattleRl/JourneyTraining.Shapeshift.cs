using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;

static partial class JourneyTraining
{
    const string ShapeshiftId="hero_skill_pool_001:06";
    static IEnumerable<Move> ShapeshiftMoves()
    {
        for(int slot=0;slot<8;slot++)foreach(bool strong in new[]{false,true})
            foreach(var target in Enum.GetValues<FixedOutputKind>())for(int option=0;option<4;option++)
            {
                yield return new("skillShapeshift",ShapeshiftId,strong?"strong":"base",Targets:(int)target,Parts:slot,Option:option);
                for(int material=0;material<8;material++)
                    if(material!=slot)yield return new("skillShapeshift",ShapeshiftId,strong?"strong":"base",Targets:(int)target,Parts:slot,Option:option,Input:"materialSlot="+material);
            }
    }
    sealed partial class Env
    {
        bool ApplyShapeshift(Move action)
        {
            if(action.Kind!="skillShapeshift")return false;
            if(!NativeShapeshift)throw new InvalidOperationException("Native Shapeshift disabled");
            var player=Journey.Player;
            if(action.Parts<0||action.Parts>=player.Deck.Hand.Count)throw new InvalidOperationException("Actual basic hand slot required");
            var owned=player.Skills.Single(s=>s.SourceId==ShapeshiftId);
            var card=player.Deck.Hand[action.Parts];bool strong=action.Choice=="strong";
            DeedCard material=null;
            if(action.Input.StartsWith("materialSlot=",StringComparison.Ordinal))
            {
                int slot=int.Parse(action.Input.Substring("materialSlot=".Length));
                if(slot<0||slot>=player.Deck.Hand.Count)throw new InvalidOperationException("Actual separate Shapeshift material required");
                material=player.Deck.Hand[slot];
            }
            if(B==null)Journey.PlayShapeshift(owned,card,strong,action.Option,(FixedOutputKind)action.Targets,material);
            else B.PlayShapeshift(owned,card,strong,action.Option,(FixedOutputKind)action.Targets,material);
            return true;
        }
        IEnumerable<Move> ShapeshiftCandidates()
        {
            if(!Journey.Player.Skills.Any(s=>s.SourceId==ShapeshiftId))yield break;
            foreach(var action in ShapeshiftMoves())
            {
                if(action.Parts>=Journey.Player.Deck.Hand.Count)continue;
                var card=Journey.Player.Deck.Hand[action.Parts];
                if(!Basics.Any(c=>c.Id==card.Id))continue;
                if(card.Id=="basic_card_023")
                {
                    if(action.Input.Length==0)continue;
                    int material=int.Parse(action.Input.Substring("materialSlot=".Length));
                    if(material>=Journey.Player.Deck.Hand.Count||Journey.Player.Deck.Hand[material].Type==CardType.Wound)continue;
                }
                else if(action.Input.Length>0)continue;
                var choices=ShapeshiftActions.Choices(card,action.Choice=="strong"||Context.NextCardEnhanced);
                if(choices.Any(c=>c.Option==action.Option&&(int)c.Kind!=action.Targets))yield return action;
            }
        }
    }
}
