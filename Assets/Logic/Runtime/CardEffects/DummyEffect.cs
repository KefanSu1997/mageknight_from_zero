using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 佔位用的空效果，供尚未實作的技能使用。
    /// </summary>
    public sealed class DummyEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0) { }
    }
}

