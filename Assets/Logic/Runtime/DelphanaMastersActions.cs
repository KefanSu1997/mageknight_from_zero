using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original Masters: spend once, then pay for different abilities while still unwounded.</summary>
    public static class DelphanaMastersActions
    {
        public static bool IsMasters(UnitState unit) => unit?.Card.Id == "army_005";
        public static bool Used(ActionContext context, UnitState unit, int ability)
            => context.MastersUsedAbilities.TryGetValue(unit, out var used) && used.Contains(ability);

        public static bool PhaseAllows(UnitAbility ability, Phase phase)
            => ability.Kind == UnitAbilityKind.StopEnemy || ability.Kind == UnitAbilityKind.DestroyBlockedEnemy
                ? phase == Phase.Block
                : (ability.Kind == UnitAbilityKind.ReduceEnemyArmor || ability.Kind == UnitAbilityKind.RemoveEnemyDefenses)
                    && (phase == Phase.Ranged || phase == Phase.Melee);

        public static bool CanContinue(ActionContext context, UnitState unit)
            => IsMasters(unit) && context.InBattle && !unit.IsDestroyed && unit.Wounds == 0
                && (unit.IsReady || context.MastersActiveThisBattle.Contains(unit))
                && unit.Card.Actions != null && Enumerable.Range(0, unit.Card.Actions.Length)
                    .Any(i => !Used(context, unit, i) && PhaseAllows(unit.Card.Actions[i], context.CombatPhase));

        public static void Use(PlayerState player, ActionContext context, UnitState unit, int index, int target)
        {
            UnitParticipation.Require(player, context, unit);
            if (!IsMasters(unit) || !player.Units.Contains(unit) || !CanContinue(context, unit))
                throw new InvalidOperationException("大师须自有、未受伤；首次能力须就绪，后续能力限本次激活");
            if (index < 0 || index >= unit.Card.Actions.Length || Used(context, unit, index))
                throw new InvalidOperationException("请选择大师本回合尚未使用的原能力");
            var ability = unit.Card.Actions[index];
            if (!PhaseAllows(ability, context.CombatPhase))
                throw new InvalidOperationException("大师此能力不能在当前战斗阶段使用");
            new CardEffects.ExposureEffect(false).Validate(player, context, target);
            var enemy = context.Enemies[target];
            bool immune = enemy.Abilities.Contains(Ability.MagicResist)
                || ability.Kind == UnitAbilityKind.StopEnemy && CombatMath.HasResistance(enemy, target, Element.Ice, context)
                || ability.Kind == UnitAbilityKind.DestroyBlockedEnemy && CombatMath.HasResistance(enemy, target, Element.Fire, context);
            if (immune) throw new InvalidOperationException("目标免疫大师所选能力");
            if (!player.Mana.TryPayExactColors(ability.Cost, context, player))
                throw new InvalidOperationException("大师所选能力所需魔力不足");

            bool affectsEnemy = ability.Kind switch
            {
                UnitAbilityKind.StopEnemy => !context.SkipAttackIndices.Contains(target),
                UnitAbilityKind.DestroyBlockedEnemy => !context.MastersDestroyWhenBlocked.Any(e => ReferenceEquals(e, enemy)),
                UnitAbilityKind.ReduceEnemyArmor => CombatMath.EffectiveArmor(enemy, target, context) > 1,
                UnitAbilityKind.RemoveEnemyDefenses => CombatMath.IsFortified(enemy, target, context)
                    || new[] { Element.Physical, Element.Fire, Element.Ice, Element.ColdFire }
                        .Any(e => CombatMath.HasResistance(enemy, target, e, context)),
                _ => false
            };
            switch (ability.Kind)
            {
                case UnitAbilityKind.StopEnemy: context.SkipAttackIndices.Add(target); break;
                case UnitAbilityKind.DestroyBlockedEnemy:
                    if (!context.MastersDestroyWhenBlocked.Any(e => ReferenceEquals(e, enemy)))
                        context.MastersDestroyWhenBlocked.Add(enemy);
                    break;
                case UnitAbilityKind.ReduceEnemyArmor: context.AddUnitArmorReduction(enemy, ability.Value); break;
                case UnitAbilityKind.RemoveEnemyDefenses:
                    context.FortificationsRemoved.Add(target); context.ResistancesRemoved.Add(target); break;
            }
            if (!context.MastersUsedAbilities.TryGetValue(unit, out var used))
                context.MastersUsedAbilities.Add(unit, used = new System.Collections.Generic.HashSet<int>());
            if (affectsEnemy) DuelActions.RecordUnitEffect(context, enemy);
            used.Add(index); context.MastersActiveThisBattle.Add(unit);
            unit.Exhaust(); context.CombatActionStarted = context.TurnActionCommitted = true;
        }
    }
}
