using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冰凍 / 致命冰凍：使目標敵人本次不攻擊，並可能降低護甲。
    /// option 為敵人索引。
    /// </summary>
    public sealed class FreezeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public FreezeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!ctx.InBattle || ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count)
                throw new System.InvalidOperationException("冰冻必须选择本场战斗中的有效敌人");
            if (ctx.DefeatedEnemyIndices.Contains(option))
                throw new System.InvalidOperationException("目标已经被击败");
            if (System.Linq.Enumerable.Contains(ctx.Enemies[option].Abilities, Ability.MagicResist))
                throw new System.InvalidOperationException("奥术免疫敌人不能成为冰冻目标");
            if (CombatMath.HasResistance(ctx.Enemies[option], option, Element.Ice, ctx))
                throw new System.InvalidOperationException("冰霜抗性敌人不能成为冰冻目标");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
