using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 效能过载：永久移除实际原行动材料，基础三次/强效两次。
    /// option属于材料的原效果；不得将其作为任意效果枚举。
    /// </summary>
    public sealed class OverloadEffect : ICardEffect, ICardEffectValidator, ICardContextualManaCost
    {
        private readonly bool _enh;
        public OverloadEffect(bool enhanced) => _enh = enhanced;

        private RepeatedCardSelection[] Selections(ActionContext ctx, int option)
        {
            int count = ctx.OverloadStepwise ? 1 : _enh ? 2 : 3;
            if (ctx.OverloadSelections == null)
                return Enumerable.Range(0, count).Select(_ => new RepeatedCardSelection
                    { Option = option, CardToDiscard = ctx.CardToDiscard }).ToArray();
            if (ctx.OverloadSelections.Count != count || ctx.OverloadSelections.Any(s => s == null))
                throw new InvalidOperationException("效能过载必须为每次原效果提供完整选择");
            return ctx.OverloadSelections.ToArray();
        }

        internal static T WithSelection<T>(ActionContext ctx, RepeatedCardSelection selection, Func<T> action)
        {
            var remove = ctx.CardToRemove;
            var discard = ctx.CardToDiscard;
            var selections = ctx.OverloadSelections;
            ctx.CardToRemove = selection.CardToRemove;
            ctx.CardToDiscard = selection.CardToDiscard;
            ctx.OverloadSelections = null;
            try { return action(); }
            finally
            {
                ctx.CardToRemove = remove;
                ctx.CardToDiscard = discard;
                ctx.OverloadSelections = selections;
            }
        }

        private ICardEffect Resolve(PlayerState player, ActionContext ctx)
        {
            var material = ctx.CardToRemove;
            if (material == null || material.Type != CardType.Action
                || !player.Deck.Hand.Any(card => ReferenceEquals(card, material)))
                throw new InvalidOperationException("效能过载必须去除实际手牌中的另一张行动牌");
            var original = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .FirstOrDefault(card => card.Id == material.Id);
            if (original == null || string.IsNullOrWhiteSpace(original.Name)
                || string.IsNullOrWhiteSpace(original.BaseEffect) || string.IsNullOrWhiteSpace(original.EnhancedEffect))
                throw new InvalidOperationException("效能过载材料必须是有效原版行动牌");
            return new ActionSystem().ResolveActionEffect(original, _enh);
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var effect = Resolve(player, ctx);
            if (ctx.OverloadStepwise)
                RepeatedEffectResolution.RequireCompleted(ctx);
            // Improvisation discards one other physical card on every use and never draws.
            // Validate the whole composite cost before removing the donor or paying the host.
            if (ctx.OverloadStepwise && effect is ImprovisationEffect)
            {
                int uses = _enh ? 2 : 3;
                int materials = player.Deck.Hand.Count(card => card.Type != CardType.Wound
                    && !ReferenceEquals(card, ctx.CardToRemove)
                    && !ReferenceEquals(card, ctx.CurrentSourceCard));
                if (materials < uses)
                    throw new InvalidOperationException("效能过载复制随机应变须备齐每次实际非创伤弃牌材料");
            }
            var selections = Selections(ctx, option);
            // Explicit consumed materials cannot be used twice, or reused after being discarded/removed.
            if (ctx.OverloadSelections != null)
            {
                var consumed = new List<DeedCard>();
                foreach (var selection in selections)
                    foreach (var card in new[] { selection.CardToRemove, selection.CardToDiscard }.Where(c => c != null))
                    {
                        if (ReferenceEquals(card, ctx.CardToRemove) || consumed.Any(c => ReferenceEquals(c, card)))
                            throw new InvalidOperationException("效能过载各次材料必须是不同实际实例，且不能重复使用原卡材料");
                        consumed.Add(card);
                    }
            }
            // The host material is thrown away, so a borrowed effect cannot consume it again.
            var material = ctx.CardToRemove;
            int materialIndex = player.Deck.Hand.FindIndex(card => ReferenceEquals(card, material));
            player.Deck.Hand.RemoveAt(materialIndex);
            ctx.CardToRemove = null;
            try
            {
                if (effect is ICardEffectValidator validator)
                    foreach (var selection in selections)
                        WithSelection(ctx, selection, () => { validator.Validate(player, ctx, selection.Option); return true; });
            }
            finally
            {
                player.Deck.Hand.Insert(materialIndex, material);
                ctx.CardToRemove = material;
            }
        }

        public ManaColor[] GetAdditionalManaCost(PlayerState player, ActionContext ctx, int option)
        {
            var effect = Resolve(player, ctx);
            var costs = new List<ManaColor>();
            foreach (var selection in Selections(ctx, option))
            {
                WithSelection(ctx, selection, () =>
                {
                    if (effect is ICardAdditionalManaCost extra) costs.AddRange(extra.GetAdditionalManaCost(selection.Option));
                    if (effect is ICardContextualManaCost contextual) costs.AddRange(contextual.GetAdditionalManaCost(player, ctx, selection.Option));
                    return true;
                });
            }
            return costs.ToArray();
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var effect = Resolve(player, ctx);
            var selections = Selections(ctx, option);
            bool stepwise = ctx.OverloadStepwise;
            ctx.OverloadStepwise = false;
            bool explicitSelections = ctx.OverloadSelections != null;
            string originalCardId=ctx.CardToRemove.Id;
            if (!player.Deck.TryRemovePermanentlyExact(ctx.CardToRemove))
                throw new InvalidOperationException("效能过载去除材料时丢失实际手牌实例");
            ctx.CardToRemove = null;
            foreach (var selection in selections)
                WithSelection(ctx, selection, () => { effect.Execute(player, ctx, selection.Option); return true; });
            if (explicitSelections)
            {
                ctx.OverloadSelections = null;
                ctx.CardToDiscard = null;
            }
            if (stepwise)
            {
                ctx.CardToDiscard = null;
                ctx.PendingRepeatedEffect = new RepeatedEffectResolution(player, effect, (_enh ? 2 : 3) - 1,originalCardId,_enh,ctx.CurrentSourceCard);
            }
        }
    }
}
