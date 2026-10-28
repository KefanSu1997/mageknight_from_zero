using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 荣誉旌旗：基础附着于一支部队；焚毁效果持续本回合，各部队限一次名望。
    /// </summary>
    public sealed class HonorBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public HonorBannerEffect(bool once) => _once = once;

        public void Assign(PlayerState player, ActionContext ctx, DeedCard instance)
            => BannerActions.AssignHonor(player, ctx.TargetUnit!, instance);

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once) throw new System.InvalidOperationException("荣誉旌旗附着必须通过真实手牌入口并选择部队");
            ctx.HonorBannersThisTurn.Add(new System.Collections.Generic.List<object>());
        }
    }
}
