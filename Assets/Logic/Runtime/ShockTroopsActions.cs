using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>原奇袭队：格挡阶段选定一次攻击；减攻、减甲与强制承伤保留各自身份。</summary>
    public static class ShockTroopsActions
    {
        public static bool IsReaction(UnitAbility ability) => ability.Kind == UnitAbilityKind.WeakenEnemyAttack
            || ability.Kind == UnitAbilityKind.InterceptEnemyAttack;

        internal static void Validate(PlayerState player, ActionContext context, UnitState unit,
            UnitAbility ability, int target, int attack)
        {
            new CardEffects.ExposureEffect(false).Validate(player, context, target);
            if (context.CombatPhase != Phase.Block) throw new InvalidOperationException("奇袭队减攻只能在格挡阶段发动");
            var enemy = context.Enemies[target];
            if (attack < 0 || attack >= enemy.AttackCount) throw new InvalidOperationException("请选择敌人的一次实际攻击");
            if (context.IsAttackCancelled(target, attack) || context.ColdStareSkips(enemy)
                || context.SuccessfullyBlockedAttacks.Contains((target, attack)))
                throw new InvalidOperationException("此攻击已取消或成功格挡");
            if (ability.Kind == UnitAbilityKind.InterceptEnemyAttack)
            {
                UnitDamageResolver.Validate(player, context, unit);
                if (context.ForcedDamageUnit(enemy, attack) != null
                    || context.ForcedDamageUnits.Any(b => ReferenceEquals(b.Unit, unit)))
                    throw new InvalidOperationException("该攻击或部队已经绑定强制承伤");
            }
        }

        internal static void Apply(ActionContext context, UnitState unit, UnitAbility ability, int target, int attack)
        {
            var enemy = context.Enemies[target];
            // Lost Legion p5: arcane immunity protects the enemy, not its individual attacks.
            context.PendingAttackReductions[(target, attack)] =
                context.PendingAttackReductions.GetValueOrDefault((target, attack)) + ability.Value;
            if (ability.Kind == UnitAbilityKind.WeakenEnemyAttack && !enemy.Abilities.Contains(Ability.MagicResist))
                context.AddUnitArmorReduction(enemy, 1);
            if (ability.Kind == UnitAbilityKind.InterceptEnemyAttack)
                context.ForcedDamageUnits.Add((enemy, attack, unit));
        }

        public static bool HasAssassination(Monster enemy, int target, ActionContext context, int attack)
            => CardCombatActions.DefenseProfile(enemy, target, context, attack).Abilities.Contains(Ability.Assassination)
                && (!context.IgnoredAbilities.TryGetValue(target, out var ignored) || ignored != Ability.Assassination);

        internal static void ValidateDamageUnit(ActionContext context, Monster enemy, int target, int attack, UnitState unit)
        {
            var forced = context.ForcedDamageUnit(enemy, attack);
            if (HasAssassination(enemy, target, context, attack) && !ReferenceEquals(forced, unit))
                throw new InvalidOperationException("刺杀伤害只能由英雄承担；仅绑定本次攻击的奇袭队可先承伤");
            if (context.ForcedDamageUnits.Any(b => ReferenceEquals(b.Unit, unit)
                && (!ReferenceEquals(b.Enemy, enemy) || b.Attack != attack)))
                throw new InvalidOperationException("此部队已保留给另一实际攻击先承伤");
        }
    }
}
