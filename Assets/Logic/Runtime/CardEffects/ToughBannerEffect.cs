using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 強韌旌旗：分配給部隊忽略一次創傷；強效非戰鬥時完全治療所有部隊。
    /// </summary>
    public sealed class ToughBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public ToughBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once) throw new System.InvalidOperationException("强韧旌旗必须通过真实手牌入口附着");
            if (ctx.InBattle) throw new System.InvalidOperationException("战斗中不能焚毁强韧旌旗治疗部队");
            foreach (var unit in player.Units)
                if (!unit.IsDestroyed && unit.Wounds > 0) CardHealing.HealUnit(player,ctx,unit,int.MaxValue);
        }
    }
}
