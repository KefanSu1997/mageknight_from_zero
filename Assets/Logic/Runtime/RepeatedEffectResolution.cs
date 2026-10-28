using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>Resumable uses of one original effect, with each choice made against the live state.</summary>
    public sealed class RepeatedEffectResolution
    {
        private readonly PlayerState _player;
        private readonly ICardEffect _effect;
        private readonly object _source;
        public int Remaining { get; private set; }
        public int Completed { get; private set; }
        public string OriginalCardId { get; }
        public bool Enhanced { get; }
        internal Action AfterCompletion;

        internal RepeatedEffectResolution(PlayerState player, ICardEffect effect, int count,string originalCardId=null,bool enhanced=false, object source=null)
        {
            _player = player;
            _effect = effect;
            _source = source;
            Remaining = count;
            Completed = 1;
            OriginalCardId=originalCardId;Enhanced=enhanced;
        }

        internal void Continue(PlayerState player, ActionContext context, RepeatedCardSelection selection)
        {
            if (!ReferenceEquals(player, _player) || !ReferenceEquals(context.PendingRepeatedEffect, this) || Remaining <= 0)
                throw new InvalidOperationException("重复效果不属于当前玩家或已完成");
            if (selection == null) throw new InvalidOperationException("必须确认本次原效果选择");
            using var source = CardContributionSource.Scope(context, _source);
            OverloadEffect.WithSelection(context, selection, () =>
            {
                if (_effect is ICardEffectValidator validator) validator.Validate(player, context, selection.Option);
                var costs = new List<ManaColor>();
                if (_effect is ICardAdditionalManaCost extra) costs.AddRange(extra.GetAdditionalManaCost(selection.Option));
                if (_effect is ICardContextualManaCost contextual) costs.AddRange(contextual.GetAdditionalManaCost(player, context, selection.Option));
                if (!player.Mana.TryPayExactColors(costs.ToArray(), context, player))
                    throw new InvalidOperationException("本次重复效果的额外魔力不足");
                _effect.Execute(player, context, selection.Option);
                return true;
            });
            Completed++;
            Remaining--;
            if (Remaining == 0)
            {
                context.PendingRepeatedEffect = null;
                var complete = AfterCompletion;
                AfterCompletion = null;
                complete?.Invoke();
            }
        }

        public static void RequireCompleted(ActionContext context)
        {
            context?.SharedSkills?.RequireNoPendingOverload(context);
            context?.SharedSkills?.RequireHowlResolved(context);
            if (context?.PendingHeadbandBorrow != null && !context.PendingHeadbandBorrow.IsExecuting)
                throw new InvalidOperationException("必须先完成启迪头环的逐次技能选择");
            if (context?.PendingRepeatedEffect != null)
                throw new InvalidOperationException("必须先完成当前卡牌的重复效果");
        }
    }
}
