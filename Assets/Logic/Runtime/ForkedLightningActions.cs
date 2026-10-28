using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public static class ForkedLightningActions
    {
        public const string SourceId = "hero_skill_pool_001:05";
        public static void Use(PlayerState player, ActionContext context, SkillCard skill, IReadOnlyList<Monster> targets)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill == null || skill.SourceId != SourceId || skill.Frequency != SkillFrequency.OncePerTurn
                || !player.Skills.Any(s => ReferenceEquals(s, skill)) || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("请选择本回合未用的自有叉形闪电");
            ExecuteCore(context, skill, targets);
            player.SkillsUsedThisTurn.Add(skill.SourceId);
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow, IReadOnlyList<Monster> targets)
        {
            borrow.RequireAuthorization(player, context);
            if (borrow.Skill.SourceId != SourceId || borrow.Skill.Frequency != SkillFrequency.OncePerTurn)
                throw new InvalidOperationException("头环借用必须对应原叉形闪电");
            ExecuteCore(context, borrow.Skill, targets);
        }

        internal static bool HasBorrowInput(ActionContext context) => context.InBattle
            && (context.CombatPhase == Phase.Ranged || context.CombatPhase == Phase.Melee)
            && context.Enemies.Select((enemy, index) => (enemy, index))
                .Any(pair => pair.enemy != null && !context.DefeatedEnemyIndices.Contains(pair.index));

        private static void ExecuteCore(ActionContext context, SkillCard skill, IReadOnlyList<Monster> targets)
        {
            if (!context.InBattle || context.CombatPhase != Phase.Ranged && context.CombatPhase != Phase.Melee)
                throw new InvalidOperationException("叉形闪电只能在远程或攻击阶段使用");
            if (targets == null || targets.Count < 1 || targets.Count > 3 || targets.Where((t, i) =>
                    t == null || targets.Take(i).Any(previous => ReferenceEquals(previous, t))).Any()
                || targets.Any(t => !context.Enemies.Select((enemy, i) => (enemy, i)).Any(pair =>
                    ReferenceEquals(pair.enemy, t) && !context.DefeatedEnemyIndices.Contains(pair.i))))
                throw new InvalidOperationException("请选择一至三名不同的本场实际存活敌人");
            foreach (var target in targets)
                context.CombatPower.AddAttack(1, Element.ColdFire, AttackType.Ranged, requiredEnemy: target, sourceSkill: skill);
        }
    }
}
