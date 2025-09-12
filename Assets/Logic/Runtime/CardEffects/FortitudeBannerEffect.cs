using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 防護旌旗：指派後提高護甲並獲得抗性，或移除本回合創傷。
    /// </summary>
    public sealed class FortitudeBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public FortitudeBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                ctx.UnitArmorBonus += 1;
                ctx.UnitsResistAll = true;
            }
            else
            {
                player.Wounds = 0;
                foreach (var u in player.Units)
                    u.Heal(int.MaxValue);
            }
        }
    }
}
