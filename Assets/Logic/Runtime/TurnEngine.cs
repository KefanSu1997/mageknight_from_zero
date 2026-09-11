namespace MK.Logic.Runtime
{
    using MK.Logic.Core;

    /// <summary>
    /// 控制回合开始时的通用刷新逻辑，例如部队就绪与摸牌。
    /// </summary>
    public sealed class TurnEngine
    {
        /// <summary>
        /// 开始玩家的新回合；部队仅在正式新一轮流程中重整。
        /// </summary>
        public void StartTurn(PlayerState player, int handLimit)
        {
            SkillActions.StartTurn(player);
            player.Deck.DrawToLimit(handLimit);
        }
    }
}
