using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>规则书第9—10页：部队承伤、抗性吸收和同场战斗一次限制。</summary>
    public static class UnitDamageResolver
    {
        public static void Validate(PlayerState player, ActionContext context, UnitState unit)
        {
            if (unit == null || !player.Units.Contains(unit)) throw new InvalidOperationException("只能让自有部队承伤");
            if (context.NoUnitDamage) throw new InvalidOperationException("当前效果禁止部队承伤");
            if (unit.IsDestroyed || unit.Wounds > 0) throw new InvalidOperationException("受伤或已销毁部队不能承伤");
            if (context.DamageAssignedUnits.Contains(unit)) throw new InvalidOperationException("同场战斗不能重复向该部队分配伤害");
        }

        public static int Assign(PlayerState player, ActionContext context, UnitState unit, Monster enemy,
            int damage, Element element, ISet<Ability> ignoredAbilities = null)
        {
            Validate(player, context, unit);
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (damage == 0) return 0;
            int armor = Math.Max(1, unit.Card.Armor + context.UnitArmorBonus);
            bool resistant = context.UnitsResistAll || (element switch
            {
                Element.Physical => unit.PhysicalResistTemp || unit.Card.Abilities.Contains(Ability.PhysicalResist),
                Element.Fire => unit.Card.Abilities.Contains(Ability.FireResist),
                Element.Ice => unit.Card.Abilities.Contains(Ability.IceResist),
                Element.ColdFire => unit.Card.Abilities.Contains(Ability.ColdFireResist)
                    || (unit.Card.Abilities.Contains(Ability.FireResist) && unit.Card.Abilities.Contains(Ability.IceResist)),
                _ => false
            });
            context.DamageAssignedUnits.Add(unit);
            if (resistant) damage = Math.Max(0, damage - armor);
            if (damage > 0)
            {
                int wound = 1;
                AbilityRules.AssignWounds(enemy, unit, ref wound, ignoredAbilities);
                if (unit.IsDestroyed) player.Units.Remove(unit);
                damage = Math.Max(0, damage - armor);
            }
            return damage;
        }

        /// <summary>调用于真实战斗结束；恢复承伤资格但不治疗、不重整。</summary>
        public static void EndBattle(PlayerState player, ActionContext context)
        {
            foreach (var unit in player.Units.Concat(context.DamageAssignedUnits).Distinct()) unit.PhysicalResistTemp = false;
            context.DamageAssignedUnits.Clear();
            context.UnitArmorBonus = 0;
            context.NoUnitDamage = false;
            context.InBattle = false;
        }
    }
}
