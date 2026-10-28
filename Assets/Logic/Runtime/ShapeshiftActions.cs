using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    public enum FixedOutputKind { Movement, Attack, Block }
    public sealed record FixedCardOutput(int Option, FixedOutputKind Kind, int Value,
        Element Element = Element.Physical, AttackType AttackType = AttackType.Melee);
    public interface IFixedCardOutput
    {
        IReadOnlyList<FixedCardOutput> FixedOutputs { get; }
    }

    /// <summary>固定项的真实生产入口；动态奖励仍留在其原效果中。</summary>
    public static class FixedCardOutputs
    {
        public static void Add(ActionContext context, FixedCardOutput output)
        {
            var target = context.ShapeshiftTarget ?? output.Kind;
            switch (target)
            {
                case FixedOutputKind.Movement: context.MovementPool += output.Value; break;
                case FixedOutputKind.Attack:
                    context.CombatPower.AddAttack(output.Value, output.Element,
                        target == output.Kind ? output.AttackType : AttackType.Melee); break;
                case FixedOutputKind.Block: context.CombatPower.AddBlock(output.Value, output.Element); break;
            }
        }
    }

    public static class ShapeshiftActions
    {
        public static IReadOnlyList<FixedCardOutput> Choices(DeedCard card, bool enhanced)
        {
            var original = Original(card);
            var effect = new ActionSystem(false).ResolveActionEffect(original, enhanced);
            return effect is IFixedCardOutput fixedEffect ? fixedEffect.FixedOutputs : Array.Empty<FixedCardOutput>();
        }

        public static void Play(PlayerState player, ActionContext context, SkillCard skill, DeedCard card,
            bool enhanced, int option, FixedOutputKind target, DeedCard material = null, AxialCoord? hex = null, TerrainType? terrain = null)
            => PlayCore(player, context, skill, card, enhanced, option, target, material, null, true, hex, terrain);

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution authorization,
            DeedCard card, bool enhanced, int option, FixedOutputKind target, DeedCard material = null, AxialCoord? hex = null, TerrainType? terrain = null)
        {
            if (authorization == null) throw new InvalidOperationException("缺少当前头环授权");
            PlayCore(player, context, authorization.Skill, card, enhanced, option, target, material, authorization, true, hex, terrain);
        }

        private static void PlayCore(PlayerState player, ActionContext context, SkillCard skill, DeedCard card,
            bool enhanced, int option, FixedOutputKind target, DeedCard material,
            HeadbandBorrowResolution authorization, bool execute, AxialCoord? hex = null, TerrainType? terrain = null)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (authorization != null)
            {
                authorization.RequireAuthorization(player, context);
                if (!ReferenceEquals(skill, authorization.Skill) || ReferenceEquals(material, authorization.PhysicalCard))
                    throw new InvalidOperationException("借用须对应实际公共技能，不能弃掉头环实体");
            }
            if (skill == null || skill.SourceId != "hero_skill_pool_001:06" || skill.Frequency != SkillFrequency.OncePerTurn ||
                authorization == null && (!player.Skills.Any(s => ReferenceEquals(s, skill)) || SkillActions.IsUsed(player, skill)))
                throw new InvalidOperationException("请选择本回合未使用的自有变换身形");
            var original = Original(card);
            if (!player.Deck.Hand.Any(c => ReferenceEquals(c, card))) throw new InvalidOperationException("必须选择实际基本行动手牌");
            var effect = new ActionSystem(false).ResolveActionEffect(original, enhanced || context.NextCardEnhanced);
            var output = (effect as IFixedCardOutput)?.FixedOutputs.SingleOrDefault(o => o.Option == option);
            if (output == null || !Enum.IsDefined(typeof(FixedOutputKind), target) || target == output.Kind)
                throw new InvalidOperationException("必须把原基本牌固定项转换为另外一种数值");
            if (target == FixedOutputKind.Movement ? context.InBattle || context.TurnActionCommitted || context.RestedThisTurn :
                !context.InBattle || context.CombatPhase != (target == FixedOutputKind.Block ? Phase.Block : Phase.Melee))
                throw new InvalidOperationException("转换后的数值不能在当前阶段使用");
            var prior = context.ShapeshiftTarget;
            var priorMaterial = context.CardToDiscard;
            var priorHex = context.TargetHex; var priorTerrain = context.TargetTerrain;
            try
            {
                context.ShapeshiftTarget = target;
                context.CardToDiscard = material;
                context.TargetHex = hex; context.TargetTerrain = terrain;
                if (execute)
                {
                    CardPlayActions.Play(player, context, card, original, enhanced, option);
                    if (authorization == null) player.SkillsUsedThisTurn.Add(skill.SourceId);
                }
                else
                {
                    if (ReferenceEquals(material, card) || ReferenceEquals(context.CardToRemove, card)
                        || context.CardsToDiscard.Any(c => ReferenceEquals(c, card)))
                        throw new InvalidOperationException("不能以当前出牌自身付款");
                    if (effect is ICardEffectValidator validator) validator.Validate(player, context, option);
                    IEnumerable<ManaColor> cost = enhanced && !context.NextCardEnhanced ? original.RequiredCrystals : Array.Empty<ManaColor>();
                    if (effect is ICardAdditionalManaCost additional) cost = cost.Concat(additional.GetAdditionalManaCost(option));
                    if (effect is ICardContextualManaCost contextual) cost = cost.Concat(contextual.GetAdditionalManaCost(player, context, option));
                    if (!player.Mana.TryPreviewExactColors(cost, context, out _)) throw new InvalidOperationException("缺少原费用");
                }
            }
            finally { context.ShapeshiftTarget = prior; context.CardToDiscard = priorMaterial; context.TargetHex = priorHex; context.TargetTerrain = priorTerrain; }
        }

        internal static bool HasBorrowInput(PlayerState player, ActionContext context, HeadbandBorrowResolution authorization)
        {
            authorization.RequireAuthorization(player, context);
            var materials = new DeedCard[] { null }.Concat(player.Deck.Hand.Where(c => !ReferenceEquals(c, authorization.PhysicalCard))).ToArray();
            foreach (var card in player.Deck.Hand.Where(c => c.Type == CardType.Action))
            foreach (bool enhanced in new[] { false, true })
            {
                IReadOnlyList<FixedCardOutput> outputs;
                try { outputs = Choices(card, enhanced || context.NextCardEnhanced); }
                catch (InvalidOperationException) { continue; }
                bool druid = card.Id == "basic_card_018";
                bool strong = enhanced || context.NextCardEnhanced;
                var hexes = druid && !strong
                    ? context.MovementMap?.Placed.Keys.Select(h => (AxialCoord?)h).ToArray() ?? Array.Empty<AxialCoord?>()
                    : new AxialCoord?[] { null };
                var terrains = druid && strong ? Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().Select(t => (TerrainType?)t).ToArray() : new TerrainType?[] { null };
                foreach (var output in outputs)
                foreach (FixedOutputKind target in Enum.GetValues(typeof(FixedOutputKind)))
                foreach (var material in materials)
                foreach (var hex in hexes)
                foreach (var terrain in terrains)
                    try
                    {
                        PlayCore(player, context, authorization.Skill, card, enhanced, output.Option, target, material, authorization, false, hex, terrain);
                        return true;
                    }
                    catch (InvalidOperationException) { }
            }
            return false;
        }

        private static ActionCardData Original(DeedCard card)
        {
            var original = card == null || card.Type != CardType.Action ? null :
                CardJsonLoader.LoadBasicActions().FirstOrDefault(c => c.Id == card.Id);
            return original ?? throw new InvalidOperationException("变换身形只支持原基本行动卡");
        }
    }
}
