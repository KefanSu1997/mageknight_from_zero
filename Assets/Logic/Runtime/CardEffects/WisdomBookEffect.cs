using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 智慧之书：去除手牌取得同色牌。
    /// </summary>
    public sealed class WisdomBookEffect : ICardEffect
    {
        private readonly bool _once;
        public WisdomBookEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToRemove == null)
                throw new System.InvalidOperationException("未指定要去除的卡牌");
            var card = ctx.CardToRemove;
            ctx.CardToRemove = null;
            player.Deck.RemoveFromHand(card);

            var color = CardColorHelper.Get(card.Id) ?? ManaColor.Red;
            if (!_once)
            {
                var supply = ctx.AdvActionSupply ?? throw new System.InvalidOperationException("缺少高級行動市場");
                int idx = supply.Offer.FindIndex(c => CardColorHelper.Get(c.Id) == color);
                if (idx < 0) idx = 0;
                var adv = supply.Take(idx);
                player.Deck.Hand.Add(new DeedCard(adv.Id, CardType.Action));
            }
            else
            {
                var supply = ctx.SpellSupply ?? throw new System.InvalidOperationException("缺少法術供應");
                int idx = supply.Offer.FindIndex(c => CardColorHelper.Get(c.Id) == color);
                if (idx < 0) idx = 0;
                var sp = supply.Claim(idx);
                player.Deck.Hand.Add(new DeedCard(sp.Id, CardType.Spell));
                player.Mana.AddCrystal(color, 1);
            }
        }
    }
}
