using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 遠古血脈：受創並取得高級行動或免費觸發其他牌強效。
    /// option 基礎為市場索引(低8位)與支付顏色(高8位)，強效為效果枚舉值。
    /// </summary>
    public sealed class AncientBloodlineEffect : ICardEffect
    {
        private readonly bool _enh;
        public AncientBloodlineEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Wounds += 1;
            if (!_enh)
            {
                var supply = ctx.AdvActionSupply ?? throw new System.InvalidOperationException("缺少高級行動市場");
                int idx = option & 0xFF;
                int colorIdx = (option >> 8) & 0xFF;
                ManaColor color = (ManaColor)System.Math.Clamp(colorIdx, 0, 3);
                // 支付任意顏色魔力
                // 模擬支付：優先標記再晶體
                if (player.Mana.Tokens.TryGetValue(color, out int t) && t > 0)
                    player.Mana.Tokens[color] = t - 1;
                else if (player.Mana.Crystals.TryGetValue(color, out int c) && c > 0)
                    player.Mana.Crystals[color] = c - 1;
                var card = supply.Take(System.Math.Clamp(idx, 0, supply.Offer.Count - 1));
                player.Deck.Hand.Add(new DeedCard(card.Id, CardType.Action));
            }
            else
            {
                var id = (ActionEffectId)option;
                CardEffectFactory.Get(id).Execute(player, ctx, option);
            }
        }
    }
}
