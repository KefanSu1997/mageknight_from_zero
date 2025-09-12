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
            if (ctx.InBattle) return;

            if (!_once)
            {
                ctx.TargetUnit?.Ready();
            }
            else
            {
                foreach (var u in player.Units)
                    u.Ready();
            }
        }
    }
}

