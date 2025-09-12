using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 靈活戰鬥：基礎三選一，強效六選一，僅處理數值累加。
    /// </summary>
    public sealed class VersatileBattleEffect : ICardEffect
    {
        private readonly bool _enh;
        public VersatileBattleEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                switch (option)
                {
                    case 0:
                        ctx.MeleePool += 2;
                        break;
                    case 1:
                        ctx.BlockPool += 2;
                        break;
                    default:
                        ctx.RangedPool += 1;
                        break;
                }
            }
            else
            {
                switch (option)
                {
                    case 0:
                        ctx.MeleePool += 4;
                        break;
                    case 1:
                        ctx.BlockPool += 4;
                        break;
                    case 2:
                        ctx.MeleePool += 3; // 火焰攻擊視作普通攻擊
                        break;
                    case 3:
                        ctx.BlockPool += 3; // 火焰格擋同理
                        break;
                    case 4:
                        ctx.RangedPool += 3;
                        break;
                    default:
                        ctx.MeleePool += 2; // 攻城攻擊僅記錄攻擊力
                        break;
                }
            }
        }
    }
}
