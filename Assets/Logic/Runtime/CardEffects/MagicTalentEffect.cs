using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔法天賦：棄一牌後可施放同色法術，強效則獲取法術牌。
    /// option 參數表示目標顏色或法術索引。
    /// </summary>
    public sealed class MagicTalentEffect : ICardEffect
    {
        private readonly bool _enh;
        public MagicTalentEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToDiscard == null)
                throw new System.InvalidOperationException("未指定要棄掉的卡牌");
            player.Deck.Discard(ctx.CardToDiscard);
            ctx.CardToDiscard = null;

            if (!_enh)
            {
                var color = (ManaColor)System.Math.Clamp(option, 0, 3);
                ctx.SpellColors.Add(color);
            }
            else
            {
                var supply = ctx.SpellSupply ?? throw new System.InvalidOperationException("缺少法術供應");
                int idx = System.Math.Clamp(option, 0, supply.Offer.Count - 1);
                var spell = supply.Claim(idx);
                player.Deck.GainToDiscard(new DeedCard(spell.Id, CardType.Spell));
            }
        }
    }
}
