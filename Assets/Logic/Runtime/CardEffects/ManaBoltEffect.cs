using MK.Logic.Core;
using MK.Logic.Data;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力箭矢 / 魔力雷矢：支付魔力後依顏色獲得攻擊。
    /// option 為顏色索引。
    /// </summary>
    public sealed class ManaBoltEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaBoltEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            var cost = new ManaCost(new Dictionary<Element,int>{{Element.Ice,1}});
            if (!player.Mana.Pay(cost, player))
                return;
            if (_enh)
            {
                ctx.RangedPool += color switch
                {
                    ManaColor.Blue => 11,
                    ManaColor.Red => 10,
                    ManaColor.White => 9,
                    ManaColor.Green => 8,
                    _ => 0
                };
            }
            else
            {
                ctx.RangedPool += color switch
                {
                    ManaColor.Blue => 8,
                    ManaColor.Red => 7,
                    ManaColor.White => 6,
                    ManaColor.Green => 5,
                    _ => 0
                };
            }
        }
    }
}
