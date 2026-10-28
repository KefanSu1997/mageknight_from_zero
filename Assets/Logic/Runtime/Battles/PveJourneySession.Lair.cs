using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public PveBattleSession EnterLair(Random rewardRandom=null)
        {
            RequireOutsideBattle();RepeatedEffectResolution.RequireCompleted(Context);
            RequireSoloCombatCardObligation();
            if(Context.TurnActionCommitted||Context.RestedThisTurn||Context.PeacefulMomentAction||Context.Lair!=null
                ||!Game.Map.Places.TryGetValue(Player.Position,out var site)
                ||(site.Type!=PlaceType.MonsterDen&&site.Type!=PlaceType.SpawningGround)||site.Conquered)
                throw new InvalidOperationException("Enter an unconquered actual lair before committing this turn's action");
            int count=site.Type==PlaceType.MonsterDen?1:2;
            var guards=site.Defenders;
            if(!ReferenceEquals(Context.EnemySupply,EnemySupply)||guards.Count!=0&&guards.Count!=count
                ||guards.Any(e=>e.TokenColor!=EnemyTokenColor.Brown||!EnemySupply.IsInUse(e)))
                throw new InvalidOperationException("Lair must retain its actual brown guard instances");
            if(guards.Count==0)
            {
                if(EnemySupply.DrawPile.Concat(EnemySupply.DiscardPile).Count(e=>e.TokenColor==EnemyTokenColor.Brown)<count)
                    throw new InvalidOperationException("Insufficient brown tokens; nothing drawn");
                site.Enemy=EnemySupply.Draw(EnemyTokenColor.Brown);
                for(int i=1;i<count;i++)site.AdditionalGuards.Add(EnemySupply.Draw(EnemyTokenColor.Brown));
            }
            site.HiddenGuards.Clear();Game.Map.RevealedSites.Add(site);
            var context=Context;context.Lair=new LairActions(Player,context,site,EnemySupply,rewardRandom);
            context.CombatSite=site;
            Battle=new PveBattleSession(Player,site.Defenders,_cards,context,_artifacts,
                canAct:()=>Game.HasActiveTurn&&!Game.IsEndingTurn&&ReferenceEquals(Context,context),spells:_spells);
            context.MovementPool=context.InfluencePool=0;context.TurnActionCommitted=true;
            return Battle;
        }
    }
}
