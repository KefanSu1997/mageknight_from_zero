using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 戰鬥號召 / 榮譽號召：臨時指揮部隊或免費招募。
    /// </summary>
    public sealed class BattleSummonsEffect : ICardEffect
    {
        private readonly bool _enh;
        public BattleSummonsEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                player.TempCommandSlots += 1;
                ctx.NoUnitDamage = true;
            }
            else
            {
                ctx.FreeRecruit = true;
            }
        }
    }
}
