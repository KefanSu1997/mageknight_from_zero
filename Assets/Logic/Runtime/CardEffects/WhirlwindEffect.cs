using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 旋風 / 颶風：使敵人不攻擊或直接消滅。
    /// option 為敵人索引。
    /// </summary>
    public sealed class WhirlwindEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public WhirlwindEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!ctx.InBattle || ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count)
                throw new InvalidOperationException("旋风必须选择本场战斗中的有效敌人");
            if (ctx.DefeatedEnemyIndices.Contains(option)) throw new InvalidOperationException("目标已经被击败");
            if (ctx.Enemies[option].Abilities.Contains(Ability.MagicResist))
                throw new InvalidOperationException("奥术免疫敌人不能成为旋风目标");
            if (_enh && ctx.CombatPhase != Phase.Melee)
                throw new InvalidOperationException("飓风只能在攻击阶段打出");
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
                ctx.SkipAttackIndices.Add(option);
        }
    }
}
