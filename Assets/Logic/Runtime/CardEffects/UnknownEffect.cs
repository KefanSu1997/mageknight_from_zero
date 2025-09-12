using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 佔位用效果：目前缺少卡牌資料，執行時不產生任何作用。
    /// </summary>
    public sealed class UnknownEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            // 無行為
        }
    }
}
