using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>消费本回合学习实际授予的能力，显式取得选中的原高级行动牌。</summary>
    public static class CardLearningActions
    {
        public static DeedCard Purchase(PlayerState player, ActionContext context, int index, bool enhanced = false)
        {
            int mode=enhanced?2:1;
            if ((context.LearningModes & mode)==0)
                throw new InvalidOperationException("没有本回合所选模式的学习能力");
            if (context.LearnUsed)
                throw new InvalidOperationException("本回合已经使用学习购牌");
            var supply=context.AdvActionSupply;
            if (supply==null || index<0 || index>=supply.Offer.Count)
                throw new InvalidOperationException("学习必须选择实际高级行动供给");
            var original=CardJsonLoader.LoadAdvancedActions().FirstOrDefault(c=>c.Id==supply.Offer[index].Id);
            if(original==null || string.IsNullOrWhiteSpace(original.Name) || string.IsNullOrWhiteSpace(original.BaseEffect))
                throw new InvalidOperationException("学习供给必须是有效原版高级行动牌");
            int cost=enhanced?9:6;
            if(context.InfluencePool<cost)
                throw new InvalidOperationException("学习购牌影响力不足");
            context.InfluencePool-=cost;
            var offered=supply.Take(index);
            var gained=new DeedCard(offered.Id,CardType.Action);
            if(enhanced)player.Deck.Hand.Add(gained);else player.Deck.GainToDiscard(gained);
            context.LearnUsed=true;
            return gained;
        }
    }
}
