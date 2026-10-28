using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloBannerKind { Courage, Fear, AssignCommand }
    public sealed record SoloBannerInput(SoloBannerKind Kind,int UnitSlot,int EnemySlot=-1,int AttackIndex=0,int PlayedSlot=-1);
    public static class SoloBannerInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloBannerInput input)=>episode.Apply(j=>
        {
            if(!j.Game.HasActiveTurn||j.Game.IsEndingTurn||input.UnitSlot<0||input.UnitSlot>=j.Player.Units.Count)
                throw new InvalidOperationException("Choose an actual owned unit during the active turn.");
            var unit=j.Player.Units[input.UnitSlot];
            if(input.Kind!=SoloBannerKind.Fear&&(input.EnemySlot!=-1||input.AttackIndex!=0)
                ||input.Kind!=SoloBannerKind.AssignCommand&&input.PlayedSlot!=-1)
                throw new InvalidOperationException("Extraneous banner parameters.");
            switch(input.Kind)
            {
                case SoloBannerKind.Courage: BannerActions.UseCourage(j.Player,j.Context,unit);break;
                case SoloBannerKind.Fear:
                    if(j.Battle==null||!j.Context.InBattle)throw new InvalidOperationException("Fear requires actual combat.");
                    j.Battle.UseFearBanner(unit,input.EnemySlot,input.AttackIndex);break;
                case SoloBannerKind.AssignCommand:
                    if(input.PlayedSlot<0||input.PlayedSlot>=j.Player.Deck.PlayedCards.Count)
                        throw new InvalidOperationException("Choose the actual played command banner.");
                    j.AssignCommandBanner(j.Player.Deck.PlayedCards[input.PlayedSlot],unit);break;
                default:throw new InvalidOperationException("Unknown banner operation.");
            }
        });
        public static IEnumerable<SoloBannerInput> Legal(SoloConquestEpisode episode)
        {
            var j=episode.Journey;
            if(episode.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn)yield break;
            var boundary=DemoStateCheckpoint.Capture(episode);
            for(int unit=0;unit<j.Player.Units.Count;unit++)
            {
                var candidates=new List<SoloBannerInput>{new(SoloBannerKind.Courage,unit)};
                if(j.Context.InBattle&&j.Battle!=null)
                    for(int enemy=0;enemy<j.Battle.Enemies.Count;enemy++)
                    for(int attack=0;attack<j.Battle.Enemies[enemy].AttackCount;attack++)
                        candidates.Add(new(SoloBannerKind.Fear,unit,enemy,attack));
                for(int played=0;played<j.Player.Deck.PlayedCards.Count;played++)
                    if(BannerActions.CommandAvailable(j.Player,j.Context,j.Player.Deck.PlayedCards[played]))
                        candidates.Add(new(SoloBannerKind.AssignCommand,unit,PlayedSlot:played));
                foreach(var input in candidates)
                {
                    bool valid=false;
                    try{Apply(episode,input);valid=true;}catch(InvalidOperationException){}
                    finally{boundary.Restore();}
                    if(valid)yield return input;
                }
            }
        }
    }
}
