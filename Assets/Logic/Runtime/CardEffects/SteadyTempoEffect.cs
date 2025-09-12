using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 穩步前進：基礎移動2並於回合結束放到底牌堆，強效移動4並放至牌堆頂。
    /// </summary>
    public sealed class SteadyTempoEffect : ICardEffect
    {
        private readonly bool _enh;
        public SteadyTempoEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _enh ? 4 : 2;
            if (_enh)
                ctx.RecycleToTop = true;
            else
                ctx.RecycleToBottom = true;
        }
    }
}
