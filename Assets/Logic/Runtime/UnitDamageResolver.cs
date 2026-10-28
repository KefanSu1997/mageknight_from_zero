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
            UnitParticipation.Require(player, context, unit);
            if (unit == null || !player.Units.Contains(unit)) throw new InvalidOperationException("只能让自有部队承伤");
            if (context.NoUnitDamage) throw new InvalidOperationException("当前效果禁止部队承伤");
            if (unit.IsDestroyed || unit.Wounds > 0) throw new InvalidOperationException("受伤或已销毁部队不能承伤");
            if (context.DamageAssignedUnits.Contains(unit)) throw new InvalidOperationException("同场战斗不能重复向该部队分配伤害");
        }

        public static int Assign(PlayerState player, ActionContext context, UnitState unit, Monster enemy,
            int damage, Element element, ISet<Ability> ignoredAbilities = null, bool assassinationException = false, Monster actualEnemy = null)
        {
            Validate(player, context, unit);
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (damage == 0) return 0;
            if (enemy.Abilities.Contains(Ability.Assassination) && ignoredAbilities?.Contains(Ability.Assassination) != true && !assassinationException)
                throw new InvalidOperationException("刺杀伤害不能分配给普通部队");
            if (context.InfluencePool < unit.Card.DamageInfluenceCost)
                throw new InvalidOperationException("向此部队分配伤害所需影响力不足");
            context.InfluencePool -= unit.Card.DamageInfluenceCost;
            context.CombatActionStarted = true;
            int armor = Math.Max(1, unit.Card.Armor + context.UnitArmorBonus + BannerActions.HonorBonus(context, unit) + BannerActions.ProtectionArmor(unit));
            bool resistant = IsResistant(context,unit,element);
            context.DamageAssignedUnits.Add(unit);
            DuelActions.RecordUnitEffect(context, enemy);
            if (resistant) damage = Math.Max(0, damage - armor);
            if (damage > 0)
            {
                int wound = 1;
                int woundsBefore = unit.Wounds;
                AbilityRules.AssignWounds(enemy, unit, ref wound, ignoredAbilities);
                if (unit.Wounds > woundsBefore && enemy.Abilities.Contains(Ability.Vampirism)
                    && ignoredAbilities?.Contains(Ability.Vampirism) != true)
                    context.Vampirism.Add(actualEnemy ?? enemy, 1);
                if (unit.IsDestroyed) player.Units.Remove(unit);
                damage = Math.Max(0, damage - armor);
            }
            return damage;
        }

        internal static int RemainingAfterAbsorption(ActionContext context,UnitState unit,int damage,Element element)
            => Math.Max(0,damage-Math.Max(1,unit.Card.Armor+context.UnitArmorBonus+BannerActions.HonorBonus(context,unit)+BannerActions.ProtectionArmor(unit))*(IsResistant(context,unit,element)?2:1));

        private static bool IsResistant(ActionContext context,UnitState unit,Element element)
            => context.UnitsResistAll || BannerActions.ProtectionResists(unit,element) || (element switch
            {
                Element.Physical => unit.PhysicalResistTemp || unit.Card.Abilities.Contains(Ability.PhysicalResist),
                Element.Fire => unit.Card.Abilities.Contains(Ability.FireResist),
                Element.Ice => unit.Card.Abilities.Contains(Ability.IceResist),
                Element.ColdFire => unit.Card.Abilities.Contains(Ability.ColdFireResist)
                    || (unit.Card.Abilities.Contains(Ability.FireResist) && unit.Card.Abilities.Contains(Ability.IceResist)),
                _ => false
            });

        /// <summary>调用于真实战斗结束；恢复承伤资格但不治疗、不重整。</summary>
        public static void EndBattle(PlayerState player, ActionContext context)
        {
            context.RampagingBattle?.Complete(player, context);
            BannerActions.ClearProtectionChoices(player);
            context.ClearDiseaseBattleState();
            context.Vampirism.Clear();
            context.ClearExplosiveBoltArmor();
            context.ClearSkillArmorReduction();
            context.BowFamePerKill = 0;
            context.CrystalsPerKill = 0;
            context.ClearKillCrystalChoices();
            // Every enemy skip registered here lasts for this combat, regardless of source.
            context.SkipAttackIndices.Clear();
            context.ClearUnitArmorReduction();
            context.CancelledAttacks.Clear();
            context.SuccessfullyBlockedAttacks.Clear();
            context.MastersActiveThisBattle.Clear();
            DuelActions.ClearBattle(context);
                NatureRevengeActions.ClearBattle(context);
            context.MastersDestroyWhenBlocked.Clear();
            context.ForcedDamageUnits.Clear();
            context.NightWingsSkippedIndices.Clear();
            context.PossessedEnemyIndices.Clear();
            context.FortificationsRemoved.Clear();
            context.ResistancesRemoved.Clear();
            // Rulebook p9: attack and block cannot be banked for a later combat.
            // Turn-duration card modifiers remain on the context until EndTurn.
            context.CombatPower.ConsumeAttacks(Phase.Melee);
            context.AllAttacksColdFire = false;
            context.AllAttacksSiege = false;
            context.CombatPower.ConsumeBlocks();
            foreach (var unit in player.Units.Concat(context.DamageAssignedUnits).Distinct()) unit.PhysicalResistTemp = false;
            context.DamageAssignedUnits.Clear();
            context.UnitArmorBonus = 0;
            context.ChargeUnitBonus = 0;
            context.CombatActionStarted = false;
            context.InBattle = false;
            context.CombatSite = null;
            context.EndBattleWounds();
            context.Dungeon?.Complete(player, context);
            context.Lair?.Complete(player, context);
            context.Ruin?.Complete(player, context);
        }
    }
}
