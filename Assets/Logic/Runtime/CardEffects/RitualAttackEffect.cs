using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 儀式攻擊：去除一張牌依顏色獲得不同攻擊。
    /// option 參數為被移除卡牌的顏色索引。
    /// </summary>
    public sealed class RitualAttackEffect : ICardEffect
    {
        private readonly bool _enh;
        public RitualAttackEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToRemove == null)
                throw new System.InvalidOperationException("未指定要移除的卡牌");
            player.Deck.RemoveFromHand(ctx.CardToRemove);
            ctx.CardToRemove = null;

            var color = (ManaColor)System.Math.Clamp(option, 0, 3);
            if (!_enh)
            {
                switch (color)
                {
                    case ManaColor.Red:
                        ctx.MeleePool += 5;
                        break;
                    case ManaColor.Blue:
                        ctx.MeleePool += 3; // 以寒冰近戰處理
                        break;
                    case ManaColor.White:
                        ctx.RangedPool += 3;
                        break;
                    default:
                        ctx.RangedPool += 2; // 攻城視同遠程
                        break;
                }
            }
            else
            {
                switch (color)
                {
                    case ManaColor.Red:
                        ctx.MeleePool += 6;
                        break;
                    case ManaColor.Blue:
                        ctx.MeleePool += 4; // 冰火近戰
                        break;
                    case ManaColor.White:
                        ctx.RangedPool += 4;
                        break;
                    default:
                        ctx.RangedPool += 3;
                        break;
                }
            }
        }
    }
}
