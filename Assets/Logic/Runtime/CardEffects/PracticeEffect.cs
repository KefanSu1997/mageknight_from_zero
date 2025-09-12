using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 修行：移除一張牌並獲取同色高級行動。
    /// option 為市場索引。
    /// </summary>
    public sealed class PracticeEffect : ICardEffect
    {
        private readonly bool _enh;
        public PracticeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToRemove == null)
                throw new System.InvalidOperationException("未指定要移除的卡牌");
            player.Deck.RemoveFromHand(ctx.CardToRemove);
            ctx.CardToRemove = null;
            var supply = ctx.AdvActionSupply ?? throw new System.InvalidOperationException("缺少高級行動市場");
            int idx = System.Math.Clamp(option, 0, supply.Offer.Count - 1);
            var card = supply.Take(idx);
            var deed = new DeedCard(card.Id, CardType.Action);
            if (_enh)
                player.Deck.Hand.Add(deed);
            else
                player.Deck.GainToDiscard(deed);
        }
    }
}
