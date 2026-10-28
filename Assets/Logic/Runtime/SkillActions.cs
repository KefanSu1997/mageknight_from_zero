using System;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Core;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>Owned skill activation and usage markers, independent of transient action contexts.</summary>
    public static class SkillActions
    {
        /// <summary>选定真实原牌、用途和费用后，一次完成技能与正式横置出牌。</summary>
        public static void PlaySideways(PlayerState player, ActionContext context, SkillCard skill,
            DeedCard card, SidewaysType type, ManaColor? color = null, bool forgoSourceDice = false)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill == null || !player.Skills.Any(s => ReferenceEquals(s, skill)) || IsUsed(player, skill))
                throw new InvalidOperationException("必须选择未使用的自有技能");
            PlaySidewaysCore(player, context, skill, card, type, color, forgoSourceDice, true);
            player.SkillsUsedThisTurn.Add(skill.SourceId);
        }

        internal static void BorrowSideways(PlayerState player, ActionContext context, HeadbandBorrowResolution borrow,
            DeedCard card, SidewaysType type, ManaColor? color, bool forgoSourceDice, bool execute)
        {
            borrow.RequireAuthorization(player, context);
            PlaySidewaysCore(player, context, borrow.Skill, card, type, color, forgoSourceDice, execute);
        }

        private static void PlaySidewaysCore(PlayerState player, ActionContext context, SkillCard skill,
            DeedCard card, SidewaysType type, ManaColor? color, bool forgoSourceDice, bool execute)
        {
            bool universal = skill.SourceId == "hero_skill_pool_005:07";
            bool repel = skill.SourceId == "hero_skill_pool_007:07";
            if ((!universal && !repel && skill.SourceId != "hero_skill_pool_007:06") || skill.Frequency != SkillFrequency.OncePerTurn)
                throw new InvalidOperationException("该技能不支持此横置入口");
            if (card == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, card))
                || !Enum.IsDefined(typeof(SidewaysType), type) || card.Type == CardType.Wound)
                throw new InvalidOperationException("必须选择真实的非伤牌手牌与横置用途");
            var basic = CardJsonLoader.LoadBasicActions().FirstOrDefault(c => c.Id == card.Id);
            var advanced = CardJsonLoader.LoadAdvancedActions().FirstOrDefault(c => c.Id == card.Id);
            var spell = CardJsonLoader.LoadSpells().FirstOrDefault(c => c.Id == card.Id);
            bool action = card.Type == CardType.Action && (basic != null || advanced != null);
            bool isSpell = card.Type == CardType.Spell && spell != null;
            bool artifact = card.Type == CardType.Artifact && CardJsonLoader.LoadItems().Any(c => c.Id == card.Id);
            if (!action && !isSpell && !artifact) throw new InvalidOperationException("卡牌类型必须与原定义一致");
            if (forgoSourceDice && !repel) throw new InvalidOperationException("仅魔法抗拒可以承诺不使用源骰");
            int value;
            if (repel)
            {
                if (color.HasValue) throw new InvalidOperationException("魔法抗拒不需要魔力费用");
                if (forgoSourceDice && (context.ManaSource?.WasUsedThisTurn(player) == true || player.HeldManaDie != null))
                    throw new InvalidOperationException("本回合已使用源骰，魔法抗拒只能横置为2");
                value = forgoSourceDice ? 3 : 2;
            }
            else if (universal)
            {
                if (!color.HasValue || !Enum.IsDefined(typeof(ManaColor), color.Value)
                    || !player.Mana.TryPreviewExactColors(new[] { color.Value }, context, out _))
                    throw new InvalidOperationException("通用力量需要支付一点实际选色魔力");
                value = (action || isSpell) && CardColorHelper.Get(card.Id) == color.Value ? 4 : 3;
                if (execute && !player.Mana.TryPayExactColors(new[] { color.Value }, context, player))
                    throw new InvalidOperationException("通用力量魔力不足");
            }
            else
            {
                if (color.HasValue) throw new InvalidOperationException("漫不经心不需要魔力费用");
                value = advanced != null || isSpell || artifact ? 3 : 2;
            }
            if (!execute) return;
            if (!new ActionSystem(false).PlaySideways(card, player, context, type, value))
                throw new InvalidOperationException("横置前实际手牌发生变化");
            if (repel && forgoSourceDice) player.ForgoSourceDiceThisTurn = true;
        }

        public static bool IsUsed(PlayerState player, SkillCard skill) => player.FlippedSkills.Contains(skill.SourceId) || (skill.Frequency switch
        {
            SkillFrequency.OncePerTurn => player.SkillsUsedThisTurn.Contains(skill.SourceId),
            SkillFrequency.OncePerRound => player.SkillsUsedThisRound.Contains(skill.SourceId),
            _ => false
        });

        public static void Use(PlayerState player, ActionContext context, SkillCard skill, int option = 0)
        {
            if (skill?.SourceId.StartsWith("hero_skill_pool_") == true && skill.SourceId.EndsWith(":09"))
                throw new InvalidOperationException("互动技能需要当前游戏的共享区及实际领取或目标输入");
            if(HeroSocialSkillRules.Handles(skill)){HeroSocialSkillRules.Use(player,context,skill,option);return;}
            if (skill == null || !player.Skills.Any(owned => ReferenceEquals(owned, skill)))
                throw new InvalidOperationException("只能使用玩家持有的技能");
            if (string.IsNullOrEmpty(skill.SourceId) || skill.Frequency == SkillFrequency.Unspecified)
                throw new InvalidOperationException("该技能的使用时机尚未接入");
            bool restoreShaman=skill.SourceId=="hero_skill_pool_003:03" && option==100;
            if (IsUsed(player, skill) && !restoreShaman)
                throw new InvalidOperationException(skill.Frequency == SkillFrequency.OncePerTurn
                    ? "该技能本回合已经使用" : "该技能本轮已经翻面使用");
            if (skill.Effect is ICardEffectValidator validator) validator.Validate(player, context, option);
            skill.Effect.Execute(player, context, option);
            // Failed validation must not consume a skill activation.
            if (skill.Frequency == SkillFrequency.OncePerTurn) player.SkillsUsedThisTurn.Add(skill.SourceId);
            else if(skill.Frequency==SkillFrequency.OncePerRound)player.SkillsUsedThisRound.Add(skill.SourceId);
        }

        internal static void StartTurn(PlayerState player)
        {
            ChaosSkillRules.RequireResolvedBeforeStartTurn(player);
            player.SkillsUsedThisTurn.Clear();
            player.ForgoSourceDiceThisTurn = false;
            player.IsPersonalTurnActive = true;
        }

        internal static void EndTurn(PlayerState player)
        {
            PuppetMasterActions.ReleaseUnkept(player);
            if(player.IsPersonalTurnActive)ChaosSkillRules.RecordEndTurn(player);
            if (player.IsPersonalTurnActive) player.CompletedPersonalTurns++;
            player.IsPersonalTurnActive = false;
            player.ForgoSourceDiceThisTurn = false;
        }
        internal static void StartRound(PlayerState player)
        {
            player.SkillsUsedThisTurn.Clear();
            player.SkillsUsedThisRound.Clear();
        }
    }
}
