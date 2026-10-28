using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 拆毁仅忽略地点城防；解离在攻击阶段实际消灭目标，二者均对适用敌人减甲。
    /// option 為目標索引（強效）。
    /// </summary>
    public sealed class DemolishEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public DemolishEffect(bool enhanced) => _enh = enhanced;

        private static bool ImmuneToRed(ActionContext ctx, int index) =>
            ctx.Enemies[index].Abilities.Contains(Ability.MagicResist)
            || CombatMath.HasResistance(ctx.Enemies[index], index, Element.Fire, ctx);

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_enh) return;
            if (!ctx.InBattle || ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count)
                throw new InvalidOperationException("解离必须选择本场战斗中的有效敌人");
            if (ctx.CombatPhase != Phase.Melee) throw new InvalidOperationException("解离只能在攻击阶段打出");
            if (ctx.DefeatedEnemyIndices.Contains(option)) throw new InvalidOperationException("目标已经被击败");
            if (ImmuneToRed(ctx, option)) throw new InvalidOperationException("火焰抗性或奥术免疫敌人不能成为解离目标");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (_enh)
            {
                CardCombatActions.DestroyEnemy(player, ctx, option);
                ctx.KillEnemyIndex = option;
            }
            else
                ctx.IgnoreSiteFortifications = true;
            if (ctx.Enemies != null)
            {
                for (int i = 0; i < ctx.Enemies.Count; i++)
                    if (!ctx.DefeatedEnemyIndices.Contains(i) && !ImmuneToRed(ctx, i))
                    {
                        ctx.ArmorReduction.TryGetValue(i, out int previous);
                        ctx.ArmorReduction[i] = previous + 1;
                    }
            }
        }
    }
}
