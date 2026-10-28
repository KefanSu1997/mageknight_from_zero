using System;
using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>启迪头环借用实际公共技能；焚毁则永久取得该供给实体。</summary>
    public sealed class HeadbandEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public HeadbandEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var supply = ctx.SkillSupply ?? throw new InvalidOperationException("缺少技能供应");
            if (option < 0 || option >= supply.Offer.Count)
                throw new InvalidOperationException("请选择实际公共技能供应中的技能");
            var skill = supply.Offer[option];
            if (skill == null || string.IsNullOrEmpty(skill.SourceId) || !skill.IsInteractive.HasValue)
                throw new InvalidOperationException("该原技能的来源分类尚未接入");
            // 取得不等于使用：焚毁允许取得互动技能，即使其复杂发动时机尚未接入。
            if (_once) { HeroSkillAcquisition.Validate(player, skill, ctx.UnitSupply); return; }
            if (HeadbandBorrowResolution.Handles(skill))
                throw new InvalidOperationException("此原技能须通过头环逐次真实输入入口使用");
            if (skill.SourceId == ChaosSkillRules.SourceId)
                throw new InvalidOperationException("混乱大师普通借用尚未接入独立盾标记；焚毁可实际取得");
            if (skill.IsInteractive.Value)
                throw new InvalidOperationException("启迪头环的普通效果不能使用互动技能");
            if (skill.Frequency != SkillFrequency.OncePerTurn && skill.Frequency != SkillFrequency.OncePerRound)
                throw new InvalidOperationException("该原技能的使用时机尚未接入启迪头环");
            if (skill.Effect is ICardEffectValidator validator)
                validator.Validate(player, ctx, ctx.HeadbandSkillOption);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var supply = ctx.SkillSupply!;
            var skill = supply.Offer[option];
            if (_once)
            {
                HeroSkillAcquisition.Acquire(player, skill, units: ctx.UnitSupply);
                supply.Offer.RemoveAt(option);
                return;
            }
            // 公共借用不改变所有权或个人使用标记，次数由头环原文决定。
            int uses = skill.Frequency == SkillFrequency.OncePerTurn ? 2 : 1;
            for (int i = 0; i < uses; i++)
                skill.Effect.Execute(player, ctx, ctx.HeadbandSkillOption);
        }
    }
}
