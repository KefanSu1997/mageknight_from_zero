namespace MK.Logic.Runtime
{
    using MK.Logic.Core;

    /// <summary>
    /// 控制回合开始时的通用刷新逻辑，例如部队就绪与摸牌。
    /// </summary>
    public sealed class TurnEngine
    {
        /// <summary>
        /// 开始玩家的新回合：所有非重创部队就绪並摸牌至上限。
        /// </summary>
        public void StartTurn(PlayerState player, int handLimit)
        {
            foreach (var u in player.Units)
                u.NewRound();
            player.Deck.DrawToLimit(handLimit);
        }
    }
}
