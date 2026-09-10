using System;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>Owned skill activation and usage markers, independent of transient action contexts.</summary>
    public static class SkillActions
    {
        public static bool IsUsed(PlayerState player, SkillCard skill) => skill.Frequency switch
        {
            SkillFrequency.OncePerTurn => player.SkillsUsedThisTurn.Contains(skill.SourceId),
            SkillFrequency.OncePerRound => player.SkillsUsedThisRound.Contains(skill.SourceId),
            _ => false
        };

        public static void Use(PlayerState player, ActionContext context, SkillCard skill, int option = 0)
        {
            if (skill == null || !player.Skills.Any(owned => ReferenceEquals(owned, skill)))
                throw new InvalidOperationException("只能使用玩家持有的技能");
            if (string.IsNullOrEmpty(skill.SourceId) || skill.Frequency == SkillFrequency.Unspecified)
                throw new InvalidOperationException("该技能的使用时机尚未接入");
            if (IsUsed(player, skill))
                throw new InvalidOperationException(skill.Frequency == SkillFrequency.OncePerTurn
                    ? "该技能本回合已经使用" : "该技能本轮已经翻面使用");
            if (skill.Effect is ICardEffectValidator validator) validator.Validate(player, context, option);
            skill.Effect.Execute(player, context, option);
            // Failed validation must not consume a skill activation.
            if (skill.Frequency == SkillFrequency.OncePerTurn) player.SkillsUsedThisTurn.Add(skill.SourceId);
            else player.SkillsUsedThisRound.Add(skill.SourceId);
        }

        internal static void StartTurn(PlayerState player) => player.SkillsUsedThisTurn.Clear();
        internal static void StartRound(PlayerState player)
        {
            player.SkillsUsedThisTurn.Clear();
            player.SkillsUsedThisRound.Clear();
        }
    }
}
