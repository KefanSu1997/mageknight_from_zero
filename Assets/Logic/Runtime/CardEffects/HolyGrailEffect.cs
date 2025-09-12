using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 黄金圣杯：治療並獲得額外效果。
    /// </summary>
    public sealed class HolyGrailEffect : ICardEffect
    {
        private readonly bool _once;
        public HolyGrailEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                int heal = System.Math.Min(2, player.Wounds);
                player.Wounds -= heal;
                player.Fame += heal;
            }
            else
            {
                int heal = System.Math.Min(6, player.Wounds);
                player.Wounds -= heal;
                ctx.DrawPerHeal = 1;
            }
        }
    }
}
