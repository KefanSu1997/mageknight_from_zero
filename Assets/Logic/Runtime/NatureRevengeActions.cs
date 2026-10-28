using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed class NatureRevengeBattleState
    {
        internal readonly List<Monster> Targets = new();
        internal readonly List<(Monster Enemy, int Attack, int Amount)> GrantedReductions = new();
        internal readonly List<(Monster Enemy, int Attack, int Amount)> SpentReductions = new();
        public IReadOnlyList<Monster> CumbersomeTargets => Targets.AsReadOnly();
    }

    /// <summary>自然复仇和笨重的实际目标及移动消费；共享标记归属/次数由外层交易管理。</summary>
    public static class NatureRevengeActions
    {
        public static bool HasCumbersome(ActionContext context, Monster enemy)
            {
            if (context == null || enemy == null) return false;
            if (context.NatureRevenge.Targets.Any(e => ReferenceEquals(e, enemy))) return true;
            int index = context.Enemies == null ? -1 : Enumerable.Range(0, context.Enemies.Count)
                .Where(i => ReferenceEquals(context.Enemies[i], enemy)).DefaultIfEmpty(-1).First();
            return enemy.Abilities.Contains(Ability.Cumbersome)
                && !context.InsightIgnores(enemy, Ability.Cumbersome)
                && !(context.IgnoredAbilities.TryGetValue(index, out var ignored) && ignored == Ability.Cumbersome);
        }
        public static int Reduction(ActionContext context, Monster enemy, int attackIndex)
            => context == null ? 0 : context.NatureRevenge.GrantedReductions.Concat(context.NatureRevenge.SpentReductions)
                .Where(r => ReferenceEquals(r.Enemy, enemy) && r.Attack == attackIndex).Sum(r => r.Amount);

        private static int Target(ActionContext context, Monster enemy, int attackIndex)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            int index = context.Enemies == null ? -1 : Enumerable.Range(0,context.Enemies.Count)
                .Where(i => ReferenceEquals(context.Enemies[i],enemy)).DefaultIfEmpty(-1).First();
            if (!context.InBattle || context.PeacefulMomentAction || context.TemporalPortalTurn || index < 0
                || (context.CombatPhase != Phase.Ranged && context.CombatPhase != Phase.Block && context.CombatPhase != Phase.AssignDamage)
                || context.DefeatedEnemyIndices.Contains(index) || attackIndex < 0 || attackIndex >= enemy.AttackCount
                || context.SkipAttackIndices.Contains(index) || context.IsAttackCancelled(index,attackIndex)
                || context.ColdStareSkips(enemy) || context.SuccessfullyBlockedAttacks.Contains((index,attackIndex)))
                throw new InvalidOperationException("必须选择本场存活敌人尚未处理的实际攻击");
            return index;
        }

        public static void ValidateGrant(ActionContext context, Monster enemy, int attackIndex = 0)
            => Target(context,enemy,attackIndex);
        public static void Grant(ActionContext context, Monster enemy, int attackIndex = 0)
        {
            int index = Target(context,enemy,attackIndex);
            if (!HasCumbersome(context,enemy)) context.NatureRevenge.Targets.Add(enemy);
            context.NatureRevenge.GrantedReductions.Add((enemy,attackIndex,1));
            context.CombatActionStarted = context.TurnActionCommitted = true;
            RecordZeroBlock(context,enemy,index,attackIndex);
        }

        public static void ValidateSpendMove(ActionContext context, Monster enemy, int attackIndex, int points)
        {
            int index = Target(context,enemy,attackIndex);
            if (context.CombatPhase != Phase.Block || !HasCumbersome(context,enemy))
                throw new InvalidOperationException("仅格挡阶段可对获得笨重的实际敌人支付移动");
            var profile = CardCombatActions.DefenseProfile(enemy,index,context,attackIndex);
            if (!context.NatureRevenge.Targets.Any(e => ReferenceEquals(e, enemy))
                && !profile.Abilities.Contains(Ability.Cumbersome))
                throw new InvalidOperationException("所选实际攻击的原笨重属性已失去，不能支付移动减攻");
            int remaining = profile.Attack;
            if (points <= 0 || points > remaining || context.MovementPool < points)
                throw new InvalidOperationException("请选择不超过剩余攻击且可实际支付的正数移动力");
        }

        public static void SpendMove(ActionContext context, Monster enemy, int attackIndex, int points)
        {
            ValidateSpendMove(context,enemy,attackIndex,points);
            int index = Enumerable.Range(0,context.Enemies.Count).First(i => ReferenceEquals(context.Enemies[i],enemy));
            context.MovementPool -= points;
            context.NatureRevenge.SpentReductions.Add((enemy,attackIndex,points));
            context.CombatActionStarted = context.TurnActionCommitted = true;
            RecordZeroBlock(context,enemy,index,attackIndex);
        }

        private static void RecordZeroBlock(ActionContext context, Monster enemy, int index, int attackIndex)
        {
            if (context.CombatPhase == Phase.Block && CardCombatActions.DefenseProfile(enemy,index,context,attackIndex).Attack == 0)
            {
                context.SuccessfullyBlockedAttacks.Add((index,attackIndex));
                context.RecordBlockedEnemy(enemy);
            }
        }

        public static void ClearBattle(ActionContext context)
            => context.NatureRevenge.SpentReductions.Clear();

        public static void ClearTurn(ActionContext context)
        {
            ClearBattle(context);
            context.NatureRevenge.Targets.Clear();
            context.NatureRevenge.GrantedReductions.Clear();
        }
    }
}
