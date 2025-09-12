using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 寒冰護體：基礎可選攻擊2或寒冰格擋3；強效固定寒冰格擋5。
    /// 進階效果中依敵人顏色或支付魔力額外格擋的部份尚未實作。
    /// </summary>
    public sealed class IcyShellEffect : ICardEffect
    {
        private readonly bool _enh;
        public IcyShellEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (option == 0)
                    ctx.MeleePool += 2;
                else
                    ctx.BlockPool += 3;
            }
            else
            {
                ctx.BlockPool += 5;
            }
        }
    }
}
