using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 自然之力：基礎賦予部隊物理抗性，強效提供攻城或格擋。
    /// option 0 攻城3，1 格擋6
    /// </summary>
    public sealed class NatureForceEffect : ICardEffect
    {
        private readonly bool _enh;
        public NatureForceEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (ctx.TargetUnit != null)
                    ctx.TargetUnit.PhysicalResistTemp = true;
            }
            else
            {
                if (option == 0)
                    ctx.RangedPool += 3; // 以攻城視為遠程
                else
                    ctx.BlockPool += 6;
            }
        }
    }
}
