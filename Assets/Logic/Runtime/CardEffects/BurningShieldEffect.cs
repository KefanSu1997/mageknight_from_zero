using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 燃燒護盾 / 爆破護盾：火焰格擋並觸發後續效果。
    /// </summary>
    public sealed class BurningShieldEffect : ICardEffect
    {
        private readonly bool _enh;
        public BurningShieldEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.BlockPool += 4;
            if (_enh)
                ctx.KillBlockedEnemy = true;
            else
                ctx.AttackAfterBlock = 4;
        }
    }
}
