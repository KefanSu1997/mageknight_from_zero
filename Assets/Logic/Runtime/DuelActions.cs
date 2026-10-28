using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed class DuelBattleState
    {
        internal readonly List<Monster> UnitAffected = new();
        internal Monster Enemy;
        internal SkillCard Skill;
        internal int Strength = 1;
        internal bool AttackGranted;
        internal bool RewardResolved;
        internal bool UnitColdFire;
        internal bool UnitSiege;
        public Monster EnemyTarget => Enemy;
        public IReadOnlyList<Monster> UnitAffectedTargets => UnitAffected.AsReadOnly();
        public bool AttackIsGranted => AttackGranted;
        public bool RewardIsResolved => RewardResolved;
        public bool TargetAffectedByUnit => Enemy != null && UnitAffected.Any(e => ReferenceEquals(e, Enemy));
    }

    /// <summary>决斗的目标、部队介入历史和击杀奖励均绑定本场真实敌人实例。</summary>
    public static class DuelActions
    {
        public const string SourceId = "hero_skill_pool_004:07";
        public static void Validate(PlayerState player, ActionContext context, SkillCard skill)
            => Validate(player, context, skill, context.SelectedEnemy, context.SelectedEnemyAttackIndex);

        private static void Validate(PlayerState player, ActionContext context, SkillCard skill, Monster enemy, int attackIndex)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill == null || skill.SourceId != SourceId || skill.Frequency != SkillFrequency.OncePerTurn || !player.Skills.Any(s => ReferenceEquals(s, skill)) || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("请选择本回合尚未使用的自有决斗技能");
            ValidateTarget(context, enemy, attackIndex);
        }

        private static void ValidateTarget(ActionContext context, Monster enemy, int attackIndex)
        {
            int index = context.Enemies == null ? -1 : Enumerable.Range(0, context.Enemies.Count).Where(i => ReferenceEquals(context.Enemies[i], enemy)).DefaultIfEmpty(-1).First();
            if (!context.InBattle || context.TemporalPortalTurn || context.PeacefulMomentAction || context.CombatPhase != Phase.Block || index < 0 || context.DefeatedEnemyIndices.Contains(index)
                || attackIndex < 0 || attackIndex >= enemy.AttackCount || context.SkipAttackIndices.Contains(index)
                || context.IsAttackCancelled(index, attackIndex) || context.SuccessfullyBlockedAttacks.Contains((index, attackIndex)))
                throw new InvalidOperationException("决斗须在格挡阶段选择本场存活敌人尚未处理的实际攻击");
        }

        public static void Use(PlayerState player, ActionContext context, SkillCard skill)
            => Use(player, context, skill, context.SelectedEnemy, context.SelectedEnemyAttackIndex);
        public static void Use(PlayerState player, ActionContext context, SkillCard skill, Monster enemy, int attackIndex = 0)
        {
            Validate(player, context, skill, enemy, attackIndex);
            Apply(context, skill, enemy, attackIndex, 1);
            player.SkillsUsedThisTurn.Add(skill.SourceId);
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow, Monster enemy, int attackIndex)
        {
            borrow.RequireAuthorization(player, context);
            if (borrow.Skill.SourceId != SourceId || borrow.Remaining != 2 || borrow.Completed != 0)
                throw new InvalidOperationException("决斗借用须一次完成同敌同攻击的双重效果");
            ValidateTarget(context, enemy, attackIndex);
            Apply(context, borrow.Skill, enemy, attackIndex, 2);
        }

        private static void Apply(ActionContext context, SkillCard skill, Monster enemy, int attackIndex, int strength)
        {
            context.CombatPower.AddBlock(strength, requiredEnemy: enemy, sourceSkill: skill, requiredAttackIndex: attackIndex);
            context.Duel.Enemy = enemy;
            context.Duel.Skill = skill;
            context.Duel.Strength = strength;
            context.Duel.AttackGranted = context.Duel.RewardResolved = false;
            context.CombatActionStarted = context.TurnActionCommitted = true;
        }

        public static void RecordUnitEffect(ActionContext context, Monster enemy)
        {
            if (context == null || enemy == null) return;
            if (!context.Duel.UnitAffected.Any(e => ReferenceEquals(e, enemy))) context.Duel.UnitAffected.Add(enemy);
        }

        public static void RecordUnitAttack(ActionContext context, Monster enemy, IEnumerable<AttackProfile> parts, Phase phase)
        {
            if (context == null) return;
            if (parts.Any(p => (p.RequiredEnemy == null || ReferenceEquals(p.RequiredEnemy, enemy)) &&
                (p.SourceUnit != null || p.FromUnitAbility || context.Duel.UnitColdFire && p.Element != Element.ColdFire
                || context.Duel.UnitSiege && phase == Phase.Ranged && p.Type != AttackType.Siege)))
                RecordUnitEffect(context, enemy);
        }

        public static void EnterPhase(ActionContext context, Phase phase)
        {
            if (context == null || phase != Phase.Melee || context.Duel.Enemy == null || context.Duel.AttackGranted) return;
            context.Duel.AttackGranted = true;
            int index = context.Enemies == null ? -1 : Enumerable.Range(0, context.Enemies.Count).Where(i => ReferenceEquals(context.Enemies[i], context.Duel.Enemy)).DefaultIfEmpty(-1).First();
            if (index >= 0 && !context.DefeatedEnemyIndices.Contains(index))
                context.CombatPower.AddAttack(context.Duel.Strength, requiredEnemy: context.Duel.Enemy, sourceSkill: context.Duel.Skill);
        }

        public static int TakeDefeatBonus(ActionContext context, Monster enemy)
        {
            if (context == null || !ReferenceEquals(context.Duel.Enemy, enemy) || context.Duel.RewardResolved) return 0;
            context.Duel.RewardResolved = true;
            return context.Duel.UnitAffected.Any(e => ReferenceEquals(e, enemy)) ? 0 : context.Duel.Strength;
        }

        public static void ClearBattle(ActionContext context)
        {
            context.Duel.UnitAffected.Clear();
            context.Duel.UnitColdFire = context.Duel.UnitSiege = false;
            context.Duel.Enemy = null;
            context.Duel.Skill = null;
            context.Duel.Strength = 1;
            context.Duel.AttackGranted = context.Duel.RewardResolved = false;
        }
    }
}
