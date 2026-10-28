using MK.Logic.Core;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冷峻目光：影響力或使敵人失去攻擊能力。
    /// option 0 影響力，1 對敵人
    /// </summary>
    public sealed class ColdStareEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ColdStareEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 && option != 1) throw new InvalidOperationException("冷峻目光必须选择影响力或敌人效果");
            if (option == 0) return;
            int index = ctx.Enemies == null ? -1 : Enumerable.Range(0, ctx.Enemies.Count)
                .Where(i => ReferenceEquals(ctx.Enemies[i], ctx.SelectedEnemy)).DefaultIfEmpty(-1).First();
            if (!ctx.InBattle || index < 0 || ctx.DefeatedEnemyIndices.Contains(index) || ctx.CombatPhase == Phase.Melee)
                throw new InvalidOperationException("必须在敌人攻击前选择本场存活敌人");
            if (ctx.SelectedEnemyAttackIndex < 0 || ctx.SelectedEnemyAttackIndex >= ctx.SelectedEnemy.AttackCount)
                throw new InvalidOperationException("所选敌人攻击序号无效");
            if (ctx.SelectedEnemy.Abilities.Contains(Ability.MagicResist)
                || CombatMath.HasResistance(ctx.SelectedEnemy, index, Element.Ice, ctx))
                throw new InvalidOperationException("目标不受蓝色敌人效果影响");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0)
            {
                ctx.InfluencePool += _enh ? 5 : 3;
            }
            else
            {
                ctx.AddColdStare(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex, _enh);
            }
            ctx.SelectedEnemy = null;
            ctx.SelectedEnemyAttackIndex = 0;
        }
    }
}
