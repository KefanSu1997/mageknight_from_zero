using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 戰鬥號召 / 榮譽號召：臨時指揮部隊或免費招募。
    /// </summary>
    public sealed class BattleSummonsEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public BattleSummonsEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh) CardUnitActions.ValidateFreeRecruit(player, ctx, option);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.OfferedUnitUses += 1;
            }
            else
            {
                CardUnitActions.RecruitFree(player, ctx, option);
            }
        }
    }
}
