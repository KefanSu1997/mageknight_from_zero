using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 榮譽旌旗：提升所有部隊攻防並在使用時獲得名望。
    /// </summary>
    public sealed class HonorBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public HonorBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.UnitArmorBonus += 1;
            ctx.UnitAttackBonus += 1;
            ctx.UnitBlockBonus += 1;
            ctx.FamePerUnitAction += 1;
        }
    }
}
