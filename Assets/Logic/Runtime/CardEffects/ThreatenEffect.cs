using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 威脅：基礎提供影響力2；強效影響力5並降低聲望1。
    /// </summary>
    public sealed class ThreatenEffect : ICardEffect
    {
        private readonly bool _enh;
        public ThreatenEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.InfluencePool += 2;
            }
            else
            {
                ctx.InfluencePool += 5;
                player.Reputation -= 1;
            }
        }
    }
}
