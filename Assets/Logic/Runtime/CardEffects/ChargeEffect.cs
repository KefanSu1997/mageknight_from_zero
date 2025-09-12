using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 衝鋒陷陣：提升所有部隊攻擊格擋，並禁止部隊受創。
    /// </summary>
    public sealed class ChargeEffect : ICardEffect
    {
        private readonly bool _enh;
        public ChargeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int val = _enh ? 3 : 2;
            ctx.UnitAttackBonus += val;
            ctx.UnitBlockBonus += val;
            ctx.NoUnitDamage = true;
        }
    }
}

