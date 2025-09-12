namespace MK.Logic.Runtime.CardEffects
{
    using MK.Logic.Runtime;

    /// <summary>
    /// 所有行動牌效果均須實作的接口。
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>
        /// 執行效果。
        /// </summary>
        /// <param name="player">目標玩家狀態</param>
        /// <param name="ctx">當前回合的臨時池</param>
        /// <param name="option">供具備多種效果的牌使用的選項參數</param>
        void Execute(PlayerState player, ActionContext ctx, int option = 0);
    }
}
