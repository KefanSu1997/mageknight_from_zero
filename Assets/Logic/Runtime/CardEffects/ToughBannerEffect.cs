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
            if (!_once)
            {
                ctx.FirstWoundIgnored = true;
            }
            else
            {
                if (!ctx.InBattle)
                {
                    foreach (var u in player.Units)
                        u.Heal(int.MaxValue);
                }
            }
        }
    }
}
