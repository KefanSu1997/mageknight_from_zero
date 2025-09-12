using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冰凍 / 致命冰凍：使目標敵人本次不攻擊，並可能降低護甲。
    /// option 為敵人索引。
    /// </summary>
    public sealed class FreezeEffect : ICardEffect
    {
        private readonly bool _enh;
        public FreezeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SkipAttackIndices.Add(option);
            if (_enh)
            {
                ctx.ArmorReduction[option] = ctx.ArmorReduction.GetValueOrDefault(option) + 4;
            }
            else
            {
                ctx.FireResistRemoved.Add(option);
            }
        }
    }
}
