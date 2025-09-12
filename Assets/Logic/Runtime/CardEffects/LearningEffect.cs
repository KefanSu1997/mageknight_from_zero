using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 學習：支付影響力取得高級行動牌，回合僅限一次。
    /// option 為市場索引。
    /// </summary>
    public sealed class LearningEffect : ICardEffect
    {
        private readonly bool _enh;
        public LearningEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.LearnUsed)
                return;
            ctx.LearnUsed = true;
            ctx.InfluencePool += _enh ? 4 : 2;
            int need = _enh ? 9 : 6;
            if (ctx.InfluencePool < need) return;
            var supply = ctx.AdvActionSupply ?? throw new System.InvalidOperationException("缺少高級行動市場");
            if (option < 0 || option >= supply.Offer.Count) return;
            ctx.InfluencePool -= need;
            var card = supply.Take(option);
            var deed = new DeedCard(card.Id, CardType.Action);
            if (_enh)
                player.Deck.Hand.Add(deed);
            else
                player.Deck.GainToDiscard(deed);
        }
    }
}
