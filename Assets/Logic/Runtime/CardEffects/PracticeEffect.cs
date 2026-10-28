using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 修行：移除一張牌並獲取同色高級行動。
    /// option 為市場索引。
    /// </summary>
    public sealed class PracticeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public PracticeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var material = ctx.CardToRemove;
            if (material == null || material.Type != CardType.Action
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, material)))
                throw new InvalidOperationException("修行必须去除实际手牌中的行动牌");
            if (!CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Any(c => c.Id == material.Id))
                throw new InvalidOperationException("修行材料必须是原版行动牌");
            var color = CardColorHelper.Get(material.Id);
            if (!color.HasValue || color.Value < ManaColor.Red || color.Value > ManaColor.White)
                throw new InvalidOperationException("修行材料必须具有原版行动牌颜色");
            var supply = ctx.AdvActionSupply;
            if (supply == null || option < 0 || option >= supply.Offer.Count)
                throw new InvalidOperationException("修行必须选择实际高级行动供给");
            var id = supply.Offer[option].Id;
            if (!CardJsonLoader.LoadAdvancedActions().Any(c => c.Id == id) || CardColorHelper.Get(id) != color)
                throw new InvalidOperationException("修行只能取得与材料同色的高级行动牌");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!player.Deck.TryRemovePermanentlyExact(ctx.CardToRemove))
                throw new InvalidOperationException("修行去除材料时丢失实际手牌实例");
            ctx.CardToRemove = null;
            var card = ctx.AdvActionSupply.Take(option);
            var deed = new DeedCard(card.Id, CardType.Action);
            if (_enh)
                player.Deck.Hand.Add(deed);
            else
                player.Deck.GainToDiscard(deed);
        }
    }
}
