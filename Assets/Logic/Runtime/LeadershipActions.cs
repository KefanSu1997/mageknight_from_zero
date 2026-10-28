using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>领导与真实部队激活合并结算；加值仅属于本次原能力。</summary>
    public static class LeadershipActions
    {
        public static void Use(PlayerState player, ActionContext context, SkillCard skill, UnitState unit,
            int abilityIndex, int option = 0, int target = -1, ManaColor[] manaColors = null,
            int boost = 0, int attackIndex = 0)
        {
            Validate(player, context, skill, unit, abilityIndex, option);
            CardUnitActions.Use(player, context, unit, abilityIndex, option, target,
                manaColors: manaColors, boost: boost, attackIndex: attackIndex, leadership: skill);
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution authorization,
            UnitState unit, int abilityIndex, int option = 0, int target = -1, ManaColor[] manaColors = null,
            int boost = 0, int attackIndex = 0)
        {
            if (authorization == null) throw new InvalidOperationException("缺少物理头环借用授权");
            CardUnitActions.Use(player, context, unit, abilityIndex, option, target,
                manaColors: manaColors, boost: boost, attackIndex: attackIndex,
                leadership: authorization.Skill, leadershipBorrow: authorization);
        }

        internal static void Validate(PlayerState player, ActionContext context, SkillCard skill,
            UnitState unit, int abilityIndex, int option, HeadbandBorrowResolution authorization = null)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (authorization != null)
            {
                authorization.RequireAuthorization(player, context);
                if (!ReferenceEquals(authorization.Skill, skill))
                    throw new InvalidOperationException("借用必须对应当前头环的实际公共技能");
            }
            if (skill == null || skill.SourceId != "hero_skill_pool_002:06" ||
                skill.Frequency != SkillFrequency.OncePerTurn ||
                authorization == null && (!player.Skills.Any(s => ReferenceEquals(s, skill)) || SkillActions.IsUsed(player, skill)))
                throw new InvalidOperationException("请选择本回合未使用的自有领导技能");
            if (unit == null || !player.Units.Contains(unit) || !unit.CanActivate ||
                unit.Card.Actions == null || abilityIndex < 0 || abilityIndex >= unit.Card.Actions.Length)
                throw new InvalidOperationException("领导需要实际就绪的自有部队及原能力");
            var ability = unit.Card.Actions[abilityIndex];
            if (option < 0 || option > (ability.Kind == UnitAbilityKind.AttackOrBlock ? 1 : 0))
                throw new InvalidOperationException("领导的部队能力选项无效");
            bool block = ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock && option == 1;
            bool attack = ability.Kind == UnitAbilityKind.Attack || ability.Kind == UnitAbilityKind.AttackOrBlock && option == 0;
            if (!context.InBattle || !block && !attack || attack && ability.AttackType == AttackType.Siege)
                throw new InvalidOperationException("领导只加值战斗中的格挡、攻击或非攻城远程能力");
            if (block ? context.CombatPhase != Phase.Block :
                context.CombatPhase != Phase.Melee && (context.CombatPhase != Phase.Ranged ||
                    ability.AttackType == AttackType.Melee && !context.AllAttacksSiege))
                throw new InvalidOperationException("此领导能力不能在当前战斗阶段发动");
        }

        internal static int Bonus(UnitAbility ability, int option) =>
            ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock && option == 1
                ? 3 : ability.AttackType == AttackType.Ranged ? 1 : 2;

        internal static bool HasBorrowInput(PlayerState player, ActionContext context, HeadbandBorrowResolution authorization)
        {
            authorization.RequireAuthorization(player, context);
            foreach (var unit in player.Units)
            for (int i = 0; i < (unit.Card.Actions?.Length ?? 0); i++)
            for (int option = 0; option <= (unit.Card.Actions[i].Kind == UnitAbilityKind.AttackOrBlock ? 1 : 0); option++)
            {
                try
                {
                    Validate(player, context, authorization.Skill, unit, i, option, authorization);
                    UnitParticipation.Require(player, context, unit);
                    var ability = unit.Card.Actions[i];
                    if (context.InfluencePool < CardUnitActions.ActivationInfluenceCost(context, unit)
                        || !player.Mana.TryPreviewExactColors(ability.Cost, context, out _)) continue;
                    if (ability.RemoveTargetFortifications || ability.RemoveTargetResistances)
                    {
                        bool target = false;
                        for (int enemy = 0; enemy < context.Enemies.Count; enemy++)
                            try { new CardEffects.ExposureEffect(false).Validate(player, context, enemy); target = true; break; }
                            catch (InvalidOperationException) { }
                        if (!target) continue;
                    }
                    return true;
                }
                catch (InvalidOperationException) { }
            }
            return false;
        }
    }
}
