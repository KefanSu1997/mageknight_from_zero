using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 寧靜時光：提供影響力，並可消耗影響力治療或重整部隊。
    /// </summary>
    public sealed class TranquilTimeEffect : ICardEffect
    {
        private readonly bool _enh;
        public TranquilTimeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += _enh ? 6 : 3;
            ctx.HealInfluenceRate = 2;
            if (_enh)
                ctx.ReadyInfluencePerLevel = 2;
        }
    }
}
