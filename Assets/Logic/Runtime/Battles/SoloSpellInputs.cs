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
        /// <summary>Spell support excluding the four solo-excluded cards.
        /// Ordered discard selections preserve physical identity and resulting deck order.</summary>
        public static IEnumerable<SoloDeedInput> SpellChoices(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal || !j.Game.HasActiveTurn || j.Game.IsEndingTurn || j.Context==null)yield break;
            var checkpoint=DemoStateCheckpoint.Capture(episode);
            for(int hand=0;hand<j.Player.Deck.Hand.Count;hand++)
            {
                var card=j.Player.Deck.Hand[hand];
                if(card.Type!=CardType.Spell || card.Id is "magic_000" or "magic_006" or "magic_012" or "magic_018")continue;
                foreach(var choice in j.Context.InBattle?j.Battle.Choices(card):j.OutsideChoices(card))
                foreach(var selection in SpellParameters(j,hand,card.Id,choice.Enhanced))
                foreach(int target in choice.Target==BattleCardTarget.None?new[]{-1}:Enumerable.Range(0,j.Battle?.Enemies.Count??0))
                foreach(int attack in choice.Target==BattleCardTarget.EnemyAttack
                    ?Enumerable.Range(0,j.Battle.Enemies[target].AttackCount):new[]{0})
                {
                    var input=new SoloDeedInput(hand,choice.Id,target,attack,selection);bool valid=false;
                    try {Apply(episode,input);valid=true;}
                    catch(InvalidOperationException) { }
                    finally {checkpoint.Restore();}
                    if(valid)yield return input;
                }
            }
        }
        internal static IEnumerable<SoloDeedSelection> SpellParameters(PveJourneySession j,int hand,string id,bool strong)
        {
            switch(id)
            {
                case "magic_004":
                    foreach(var path in SpellPaths(j,j.Player.Position,3,true))yield return new(TravelPath:path);
                    yield break;
                case "magic_023" when strong:
                    foreach(var enemies in MaterialSubsets(Enumerable.Range(0,j.Battle?.Enemies.Count??0)
                        .Where(i=>!j.Context.DefeatedEnemyIndices.Contains(i)).ToArray(),0,new List<int>()))
                        if(enemies.Length>0&&enemies.Length*(enemies.Length-1)/2<=j.Context.MovementPool)
                            yield return new(EnemyIndices:enemies);
                    yield break;
                case "magic_023":
                    foreach(var path in SpellPaths(j,j.Player.Position,Math.Min(5,Math.Max(0,j.Context.MovementPool)),false))
                        if(path.Length>0)yield return new(TravelPath:path);
                    yield break;
                case "magic_002" when strong:
                    foreach(var cards in DieSequences(j.Player.Deck.DiscardPile.Count,Math.Min(2,j.Player.Deck.DiscardPile.Count),new List<int>()))
                        yield return new(DiscardSlots:cards);yield break;
                case "magic_005" when strong:
                    foreach(var units in MaterialSubsets(Enumerable.Range(0,j.Player.Units.Count).ToArray(),0,new List<int>()))
                        yield return new(UnitSlots:units);yield break;
                case "magic_011" when strong:
                    foreach(int first in new[]{(int)ManaColor.Green,(int)ManaColor.White})
                    foreach(int second in new[]{(int)ManaColor.Red,(int)ManaColor.Blue})
                        yield return new(BonusChoices:new[]{first,second});yield break;
                case "magic_011":
                    foreach(var materials in MaterialSubsets(Enumerable.Range(0,j.Player.Deck.Hand.Count).Where(i=>i!=hand).ToArray(),0,new List<int>()))
                    {
                        if(materials.Length>3)continue;
                        int artifacts=materials.Count(i=>j.Player.Deck.Hand[i].Type==CardType.Artifact);
                        foreach(var colors in CrystalBonuses(artifacts,new List<int>()))
                            yield return new(HandSlots:materials,BonusChoices:colors);
                    }
                    yield break;
                case "magic_020" when strong:
                    for(int offered=0;offered<j.Game.Globals.Units.Offer.Count;offered++)
                    for(int disband=-1;disband<j.Player.Units.Count;disband++)
                        yield return new(OfferedUnitSlot:offered,DisbandUnitSlot:disband);yield break;
                default:yield return new();yield break;
            }
        }
        private static IEnumerable<AxialCoord[]> SpellPaths(PveJourneySession j,AxialCoord from,int remaining,bool tunnel)
        {
            yield return Array.Empty<AxialCoord>();
            if(remaining==0)yield break;
            foreach(var direction in AxialCoord.NeighborDirs)
            {
                var next=from+direction;
                if(!j.Game.Map.Placed.TryGetValue(next,out var tile)||tile.Edges.Length==0
                    ||tunnel&&(tile.Edges[0]==TerrainType.Swamp||tile.Edges[0]==TerrainType.Lake))continue;
                foreach(var suffix in SpellPaths(j,next,remaining-1,tunnel))yield return new[]{next}.Concat(suffix).ToArray();
            }
        }
        private static IEnumerable<int[]> CrystalBonuses(int count,List<int> prefix)
        {
            if(count==0){yield return prefix.ToArray();yield break;}
            for(int c=0;c<4;c++)
            {prefix.Add(c);foreach(var x in CrystalBonuses(count-1,prefix))yield return x;prefix.RemoveAt(prefix.Count-1);}
        }
    }
}
