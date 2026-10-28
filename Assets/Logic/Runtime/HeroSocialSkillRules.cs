using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>原社会技能的持久翻面与本人本回合交易许可。</summary>
    public static class HeroSocialSkillRules
    {
        public const string Disguise = "hero_skill_pool_003:05";
        public const string LoneWolf = "hero_skill_pool_004:03";
        public static bool Handles(SkillCard skill) => skill != null && (skill.SourceId == Disguise || skill.SourceId == LoneWolf);
        public static (int Option, string Label)[] Options(PlayerState player, SkillCard skill) => !Handles(skill)
            ? Array.Empty<(int, string)>() : skill.SourceId == Disguise
            ? player.FlippedSkills.Contains(Disguise) ? new[] { (100, "支付绿魔力翻回（不重置次数）") }
                : new[] { (0, "影响力2"), (1, "翻面：本回合忽略声誉轨作用") }
            : new[] { (0, "影响力1"), (1, "影响力3：本回合不以影响力招募") };
        public static bool ReputationEffectsIgnored(ActionContext context) => context?.IgnoreReputationTrackEffects == true;

        public static void Use(PlayerState player, ActionContext context, SkillCard skill, int option = 0)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (!Handles(skill) || !player.Skills.Any(s => ReferenceEquals(s, skill)))
                throw new InvalidOperationException("必须选择实际持有的原社会技能");
            if (skill.SourceId == Disguise && option == 100)
            {
                if (!player.FlippedSkills.Contains(Disguise)) throw new InvalidOperationException("奥术伪装尚未翻面");
                if (!player.Mana.TryPayExactColors(new[] { ManaColor.Green }, context, player))
                    throw new InvalidOperationException("翻回奥术伪装须支付一点绿色魔力");
                player.FlippedSkills.Remove(Disguise);
                return;
            }
            if (option < 0 || option > 1 || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("技能本回合已经使用、仍翻面或选项无效");
            ExecuteCore(player, context, skill, option);
            player.SkillsUsedThisTurn.Add(skill.SourceId);
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow, int option = 0)
        {
            borrow.RequireAuthorization(player, context);
            ValidateBorrowChoice(borrow.Skill, option);
            ExecuteCore(player, context, borrow.Skill, option);
        }

        internal static bool HasBorrowInput(ActionContext context, SkillCard skill)
        {
            try { ValidateBorrowChoice(skill, 0); ValidateCore(context, skill, 0); return true; }
            catch (InvalidOperationException) { return false; }
        }

        private static void ValidateBorrowChoice(SkillCard skill, int option)
        {
            if (!Handles(skill) || skill.Frequency != SkillFrequency.OncePerTurn
                || skill.SourceId == Disguise && option != 0)
                throw new InvalidOperationException("分次影响力入口仅支持普通分支；翻面效果请用独立头环选择");
        }

        private static void ValidateCore(ActionContext context, SkillCard skill, int option)
        {
            if (option < 0 || option > 1) throw new InvalidOperationException("请选择原社会技能的有效选项");
            if (context.RestedThisTurn || context.InBattle && context.CombatPhase == Phase.End ||
                context.TurnActionCommitted && !context.InNegotiation && !context.InBattle)
                throw new InvalidOperationException("请在行动前或当前交涉中使用社会技能");
            if (skill.SourceId == LoneWolf && option == 1 && context.InfluenceRecruitmentOccurred)
                throw new InvalidOperationException("本回合已经以影响力招募，不能承诺不招募");
        }

        internal static void BorrowFlip(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow, int option)
        {
            borrow.RequireAuthorization(player, context);
            if (borrow.Skill.SourceId != Disguise) throw new InvalidOperationException("请选择原奥术伪装");
            ExecuteCore(player, context, borrow.Skill, option, true);
        }

        private static void ExecuteCore(PlayerState player, ActionContext context, SkillCard skill, int option, bool borrowed = false)
        {
            ValidateCore(context, skill, option);
            if (skill.SourceId == Disguise)
            {
                if (option == 0) context.InfluencePool += 2;
                else
                {
                    // Remove exactly the already-applied frozen track modifier, including reversed/doubled cases.
                    if (context.InteractionReputationModifier.HasValue)
                    {
                        context.InfluencePool -= context.InteractionAppliedModifier;
                        context.InteractionReputationModifier = 0;
                        context.InteractionAppliedModifier = 0;
                    }
                    context.IgnoreReputationTrackEffects = true;
                    if (!borrowed) player.FlippedSkills.Add(Disguise);
                }
            }
            else
            {
                context.InfluencePool += option == 1 ? 3 : 1;
                if (option == 1) context.ForbidInfluenceRecruitment = true;
            }
        }

        public static void ClearTurn(ActionContext context)
        {
            context.IgnoreReputationTrackEffects = false;
            context.InfluenceRecruitmentOccurred = false;
            context.ForbidInfluenceRecruitment = false;
        }
    }
}
