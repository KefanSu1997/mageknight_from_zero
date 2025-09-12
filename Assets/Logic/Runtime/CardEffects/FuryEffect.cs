using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 狂怒：基礎可選攻擊或格擋2，強效提供攻擊4。
    /// </summary>
    public sealed class FuryEffect : ICardEffect
    {
        private readonly bool _enh;
        public FuryEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (option == 0)
                    ctx.MeleePool += 2;
                else
                    ctx.BlockPool += 2;
            }
            else
            {
                ctx.MeleePool += 4;
            }
        }
    }
}
