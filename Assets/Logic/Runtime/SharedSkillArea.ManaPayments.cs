using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed record OverloadOutputChoice(string Label, AttackProfile Attack = null, BlockAllocation Block = null, bool Movement = false, bool Influence = false);

    public sealed partial class SharedSkillArea
    {
        public const string ManaOverloadId = "hero_skill_pool_007:09";
        private sealed record EnhancementArm(PlayerState Owner, SkillCard Skill, ManaColor Color);
        private sealed record OverloadResolution(PlayerState Player, SharedSkillToken Token, OverloadOutputChoice[] Choices);
        private readonly Dictionary<ActionContext, EnhancementArm> _enhancementArms = new();
        private readonly Dictionary<ActionContext, OverloadResolution> _overloadResolutions = new();

        /// <summary>Public selected payment color; selection alone is not skill use.</summary>
        public ManaColor? PendingEnhancementColor(ActionContext context)
            => context != null && _enhancementArms.TryGetValue(context, out var arm) ? arm.Color : null;

        public void ArmEnhancement(PlayerState player, ActionContext context, SkillCard skill, ManaColor color)
        {
            RequireUse(player, context, skill, ManaEnhanceId);
            if (!FamiliarActions.IsBasicColor(color)) throw new InvalidOperationException("魔力强化需要实际支付的基本色");
            _enhancementArms[context] = new EnhancementArm(player, skill, color);
        }

        public void CancelEnhancement(PlayerState player, ActionContext context)
        { RequireActual(player, context); _enhancementArms.Remove(context); }

        internal void ValidateManaPayment(PlayerState player, ActionContext context, IReadOnlyList<ManaColor> paid)
        {
            if (!_enhancementArms.TryGetValue(context, out var arm) || !paid.Contains(arm.Color)) return;
            if (!ReferenceEquals(player, arm.Owner) || !arm.Owner.Skills.Any(s => ReferenceEquals(s, arm.Skill))
                || SkillActions.IsUsed(arm.Owner, arm.Skill) || _tokens.Any(t => ReferenceEquals(t.Skill, arm.Skill)))
                throw new InvalidOperationException("预选魔力强化已不可用，请取消或重新选择");
        }

        internal void OnManaPaid(PlayerState player, ActionContext context, IReadOnlyList<ManaColor> paid)
        {
            if (!_enhancementArms.TryGetValue(context, out var arm) || !ReferenceEquals(player, arm.Owner) || !paid.Contains(arm.Color)) return;
            _enhancementArms.Remove(context);
            Publish(player, arm.Skill).Color = arm.Color;
            player.Mana.AddCrystal(arm.Color);
        }

        public void ClaimEnhancement(PlayerState player, ActionContext context, SharedSkillToken token)
        {
            RequireClaim(player, context, token, ManaEnhanceId);
            player.Mana.AddToken(token.Color.Value);
            Return(token);
        }

        public SharedSkillToken UseOverload(PlayerState player, ActionContext context, SkillCard skill, ManaColor color)
        {
            RequireUse(player, context, skill, ManaOverloadId);
            if (!Enum.IsDefined(typeof(ManaColor), color) || color == ManaColor.Gold)
                throw new InvalidOperationException("魔力霸主只能选择非金颜色");
            player.Mana.AddToken(color);
            var token = Publish(player, skill); token.Color = color; return token;
        }

        internal SharedSkillToken EligibleOverload(IReadOnlyList<ManaColor> paid)
            => _tokens.FirstOrDefault(t => t.Skill.SourceId == ManaOverloadId && t.Color.HasValue && paid.Contains(t.Color.Value));

        internal void CompleteOverload(PlayerState player, ActionContext context, SharedSkillToken token,
            object source, bool movement, bool influence, IEnumerable<AttackProfile> attacks, IEnumerable<BlockAllocation> blocks)
        {
            if (token == null || source == null || !_tokens.Any(t => ReferenceEquals(t, token))) return;
            var choices = new List<OverloadOutputChoice>();
            if (movement) choices.Add(new OverloadOutputChoice("本牌移动 +4", Movement: true));
            if (influence) choices.Add(new OverloadOutputChoice("本牌影响 +4", Influence: true));
            choices.AddRange(attacks.Where(a => a.Value > 0 && ReferenceEquals(a.SourceCard, source) && !a.FromUnitAbility)
                .Select(a => new OverloadOutputChoice($"本牌 {a.Element} {a.Type} {a.Value} +4", Attack: a)));
            choices.AddRange(blocks.Where(b => b.Value > 0 && ReferenceEquals(b.SourceCard, source) && !b.FromUnitAbility)
                .Select(b => new OverloadOutputChoice($"本牌 {b.Element} 格挡 {b.Value} +4", Block: b)));
            if (choices.Count == 0) return;
            _overloadResolutions.Add(context, new OverloadResolution(player, token, choices.ToArray()));
            if (choices.Count == 1) ApplyOverload(context, 0);
        }

        public IReadOnlyList<OverloadOutputChoice> PendingOverloadChoices(ActionContext context)
            => _overloadResolutions.TryGetValue(context, out var pending) ? Array.AsReadOnly(pending.Choices) : Array.Empty<OverloadOutputChoice>();

        public void RequireNoPendingOverload(ActionContext context)
        {
            if (context != null && _overloadResolutions.ContainsKey(context))
                throw new InvalidOperationException("请先选择魔力霸主为这张实际卡牌的哪一项贡献加4");
        }

        public void ResolveOverload(PlayerState player, ActionContext context, int choice)
        {
            // RequireActual also requires completed choices, so validate the actual turn directly here.
            if (player == null || context == null || !_isActualTurn(player, context)
                || !_overloadResolutions.TryGetValue(context, out var pending) || !ReferenceEquals(pending.Player, player))
                throw new InvalidOperationException("没有属于当前玩家的魔力霸主选择");
            ApplyOverload(context, choice);
        }

        private void ApplyOverload(ActionContext context, int index)
        {
            var pending = _overloadResolutions[context];
            if (index < 0 || index >= pending.Choices.Length) throw new InvalidOperationException("请选择本牌实际贡献");
            var choice = pending.Choices[index];
            if (choice.Attack != null) context.CombatPower.BoostExactAttack(choice.Attack, 4);
            else if (choice.Block != null) context.CombatPower.BoostExactBlock(choice.Block, 4);
            else if (choice.Movement) context.MovementPool += 4;
            else if (choice.Influence) context.InfluencePool += 4;
            Return(pending.Token); _overloadResolutions.Remove(context);
        }

        public void ClearManaSelections(ActionContext context)
        { RequireNoPendingOverload(context); if (context != null) _enhancementArms.Remove(context); }
    }
}
