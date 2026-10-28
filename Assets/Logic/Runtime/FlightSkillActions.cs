using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime
{
    public static class FlightSkillActions
    {
        public static void Move(PlayerState player, ActionContext context, SkillCard skill, AxialCoord target,
            IEnumerable<PlayerState> otherPlayers = null)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill == null || skill.SourceId != "hero_skill_pool_005:06" || skill.Frequency != SkillFrequency.OncePerRound
                || !player.Skills.Any(s => ReferenceEquals(s, skill)) || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("飞行须选择本轮尚未翻面的自有技能");
            MoveCore(player, context, target, otherPlayers);
            player.SkillsUsedThisRound.Add(skill.SourceId);
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow,
            AxialCoord target, IEnumerable<PlayerState> otherPlayers = null)
        {
            borrow.RequireAuthorization(player, context);
            if (borrow.Skill.SourceId != "hero_skill_pool_005:06" || borrow.Skill.Frequency != SkillFrequency.OncePerRound)
                throw new InvalidOperationException("头环借用必须对应原飞行");
            MoveCore(player, context, target, otherPlayers);
        }

        private static void MoveCore(PlayerState player, ActionContext context, AxialCoord target, IEnumerable<PlayerState> otherPlayers)
        {
            if (!new MovementService().TryFlightUsingPool(player, target, context, otherPlayers))
                throw new InvalidOperationException("飞行须选择一格安全落点或支付2移动力到两格安全落点，且回合仍可移动");
        }
    }
}
