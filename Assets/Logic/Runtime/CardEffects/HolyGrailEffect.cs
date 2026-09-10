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
                int heal = CardHealing.Heal(player, ctx, 2);
                player.Fame += heal;
            }
            else
            {
                ctx.DrawPerHeal = 1;
                CardHealing.Heal(player, ctx, 6);
            }
        }
    }
}
