using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 指挥旌旗：影响力4；实际附着在本回合新招募部队上代替其指挥标记。
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
            }
            else
            {
                CardUnitActions.RecruitFree(player, ctx, option);
                player.Fame += 2;
            }
        }
    }
}
