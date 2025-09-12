using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 拆毀 / 解離：忽略城防並降低護甲，強效可瞬殺一名敵人。
    /// option 為目標索引（強效）。
    /// </summary>
    public sealed class DemolishEffect : ICardEffect
    {
        private readonly bool _enh;
        public DemolishEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.IgnoreFortified = true;
            if (_enh)
                ctx.KillEnemyIndex = option;
            if (ctx.Enemies != null)
            {
                for (int i = 0; i < ctx.Enemies.Count; i++)
                    ctx.ArmorReduction[i] = System.Math.Max(ctx.ArmorReduction.GetValueOrDefault(i), 1);
            }
        }
    }
}
