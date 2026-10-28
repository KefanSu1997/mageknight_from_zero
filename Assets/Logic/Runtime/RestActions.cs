using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Printed standard rest / slow recovery over actual hand instances, not healing.</summary>
    public static class RestActions
    {
        public static void Rest(PlayerState player,ActionContext context,DeedCard ordinary,IEnumerable<DeedCard> wounds)
        {
            if(context.InBattle || context.TurnActionCommitted || context.MovedThisTurn || context.RestedThisTurn)
                throw new InvalidOperationException("休息回合不能移动、战斗或交涉，也不能重复休息");
            RepeatedEffectResolution.RequireCompleted(context);
            var chosen=wounds?.ToArray()??Array.Empty<DeedCard>();
            bool hasOrdinary=player.Deck.Hand.Any(c=>c.Type!=CardType.Wound);
            if(chosen.Any(c=>c==null || c.Type!=CardType.Wound || !player.Deck.Hand.Any(h=>ReferenceEquals(h,c)))
                || chosen.Where((c,i)=>chosen.Take(i).Any(other=>ReferenceEquals(c,other))).Any()
                || hasOrdinary && (ordinary==null || ordinary.Type==CardType.Wound || !player.Deck.Hand.Any(h=>ReferenceEquals(h,ordinary)))
                || !hasOrdinary && (ordinary!=null || chosen.Length!=1))
                throw new InvalidOperationException("标准休息须选一张实际非创伤牌和任意伤牌；只有伤牌时必须仅选一张伤牌缓慢恢复");
            if(ordinary!=null)player.Deck.TryDiscardExact(ordinary);
            foreach(var wound in chosen)player.Deck.TryDiscardExact(wound);
            context.RestedThisTurn=context.TurnActionCommitted=true;
            player.FlippedSkills.Remove("hero_skill_pool_003:02");
        }
    }
}
