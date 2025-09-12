using MK.Logic.Core;
using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 恐懼旌旗：取消敵人攻擊。
    /// option 為欲跳過攻擊的敵人索引。
    /// </summary>
    public sealed class FearBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public FearBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                if (ctx.Enemies != null && option >= 0 && option < ctx.Enemies.Count)
                {
                    ctx.SkipAttackIndices.Add(option);
                    player.Fame += 1;
                }
            }
            else
            {
                if (ctx.Enemies != null)
                {
                    int count = Math.Min(3, ctx.Enemies.Count);
                    for (int i = 0; i < count; i++)
                        ctx.SkipAttackIndices.Add(i);
                }
            }
        }
    }
}
