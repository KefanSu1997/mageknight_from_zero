using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 指揮旌旗：提供影響力並臨時增加指揮槽，強效免費招募並獲得名望。
    /// </summary>
    public sealed class CommandBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public CommandBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                ctx.InfluencePool += 4;
                player.TempCommandSlots += 1;
            }
            else
            {
                player.Fame += 2;
                ctx.FreeRecruit = true;
            }
        }
    }
}
