using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Spend card-granted influence on an owned unit, independently of its wound state.</summary>
    public static class CardUnitActions
    {
        public static void Use(PlayerState player, ActionContext context, UnitState unit, int abilityIndex, int option = 0)
        {
            if (unit == null || !player.Units.Contains(unit)) throw new InvalidOperationException("只能发动自有部队");
            if (!unit.CanActivate) throw new InvalidOperationException("部队必须就绪、未受伤且未销毁");
            var abilities = unit.Card.Actions;
            if (abilities == null || abilityIndex < 0 || abilityIndex >= abilities.Length)
                throw new InvalidOperationException("部队能力尚未接入或选择无效");
            var ability = abilities[abilityIndex];
            if (option < 0 || option > (ability.Kind == UnitAbilityKind.AttackOrBlock ? 1 : 0))
                throw new InvalidOperationException("部队能力选项无效");
            if (!context.InBattle && (ability.Kind == UnitAbilityKind.Attack || ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock))
                throw new InvalidOperationException("攻击和格挡能力必须在战斗中发动");
            if (!player.Mana.TryPayExactColors(ability.Cost, context, player))
                throw new InvalidOperationException("部队能力所需魔力不足");
            switch (ability.Kind)
            {
                case UnitAbilityKind.Attack: context.CombatPower.AddAttack(ability.Value, ability.Element, ability.AttackType); break;
                case UnitAbilityKind.Block: context.CombatPower.AddBlock(ability.Value, ability.Element); break;
                case UnitAbilityKind.AttackOrBlock:
                    if (option == 0) context.CombatPower.AddAttack(ability.Value, ability.Element);
                    else context.CombatPower.AddBlock(ability.Value, ability.Element);
                    break;
                case UnitAbilityKind.Movement: context.MovementPool += ability.Value; break;
                case UnitAbilityKind.Influence: context.InfluencePool += ability.Value; break;
            }
            unit.Exhaust();
            if (ability.SelfWounds > 0) unit.AddWounds(ability.SelfWounds);
        }

        public static bool TryRecruit(PlayerState player, ActionContext context, IList<UnitCard> offer, int index, RecruitLocation location)
        {
            if (index < 0 || index >= offer.Count) return false;
            var card = offer[index];
            int cost = RecruitmentService.GetCost(player, card, location, context);
            if (!new RecruitmentService().TryRecruit(player, card, location, context.InfluencePool, context)) return false;
            context.InfluencePool -= cost;
            offer.RemoveAt(index);
            return true;
        }

        public static bool TryReady(PlayerState player, ActionContext context, UnitState unit)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.IsReady
                || context.ReadyInfluencePerLevel <= 0 || unit.Card.Level < 1
                || unit.Card.Level > context.ReadyUnitMaxLevel) return false;
            int cost = unit.Card.Level * context.ReadyInfluencePerLevel;
            if (context.InfluencePool < cost) return false;
            context.InfluencePool -= cost;
            unit.Ready();
            return true;
        }
    }
}
