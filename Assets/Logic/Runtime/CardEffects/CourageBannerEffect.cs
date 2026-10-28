using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 勇氣旌旗：重整部隊，僅限非戰鬥時使用。
    /// </summary>
    public sealed class CourageBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public CourageBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once) throw new System.InvalidOperationException("勇气旌旗必须通过真实手牌入口附着");
            if (ctx.InBattle) throw new System.InvalidOperationException("战斗中不能焚毁勇气旌旗重整部队");
            foreach (var unit in player.Units)
                unit.Ready();
        }
    }
}
