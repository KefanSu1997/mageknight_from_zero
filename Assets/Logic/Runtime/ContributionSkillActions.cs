using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original skills modify a selected live contribution, preserving its element and rewards.</summary>
    public static class ContributionSkillActions
    {
        public static void Bullseye(PlayerState player, ActionContext context, SkillCard skill, AttackProfile attack)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill?.SourceId != "hero_skill_pool_004:04" || !player.Skills.Any(s => ReferenceEquals(s, skill))
                || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("请选择本回合未使用的自有百步穿杨");
            BullseyeCore(context, attack);
            player.SkillsUsedThisTurn.Add(skill.SourceId);
        }

        internal static void BorrowBullseye(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow, AttackProfile attack)
        {
            borrow.RequireAuthorization(player, context);
            BullseyeCore(context, attack);
        }

        private static void BullseyeCore(ActionContext context, AttackProfile attack)
        {
            if (!context.InBattle || context.CombatPhase != Phase.Ranged && context.CombatPhase != Phase.Melee
                || attack?.SourceCard == null || attack.SourceUnit != null
                || !context.CombatPower.AvailableAttacks(context.CombatPhase, context.AllAttacksSiege).Any(a => ReferenceEquals(a, attack)))
                throw new InvalidOperationException("请选择当前阶段真实原牌提供且尚未消费的攻击");
            context.CombatPower.BoostExactAttack(attack, context.CombatPhase == Phase.Ranged ? 1 : 2);
        }

        public static void ApplySoulGuideBlock(PlayerState player, ActionContext context, BlockAllocation block)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (!context.SoulGuideBlockPending || !context.InBattle || context.CombatPhase != Phase.Block)
                throw new InvalidOperationException("灵魂向导需要本回合未分配的格挡加值，并处于格挡阶段");
            context.CombatPower.BoostExactBlock(block, 1);
            context.SoulGuideBlockPending = false;
        }
    }
}
