using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 穩步前進：移動2/4；回合末可選將實際本卡放到底/頂，基礎須牌庫非空。
    /// </summary>
    public sealed class SteadyTempoEffect : ICardEffect
    {
        private readonly bool _enh;
        public SteadyTempoEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _enh ? 4 : 2;
        }

        public static void SelectReturn(PlayerState player, ActionContext ctx, DeedCard instance, bool recycle)
        {
            if (!ctx.SteadyTempoPlayed.Any(p => ReferenceEquals(p.card, instance))
                || !player.Deck.PlayedCards.Any(c => ReferenceEquals(c, instance)))
                throw new InvalidOperationException("只能选择本回合实际打出的稳步前进原卡");
            if (recycle) ctx.SteadyTempoReturns.Add(instance);
            else ctx.SteadyTempoReturns.Remove(instance);
        }

        internal static void ResolveReturn(PlayerState player, ActionContext ctx)
        {
            if (ctx == null) return;
            foreach (var (card, enhanced) in ctx.SteadyTempoPlayed)
            {
                if (!ctx.SteadyTempoReturns.Contains(card) || (!enhanced && player.Deck.DrawPileCount == 0)) continue;
                if (!player.Deck.TryTakePlayedExact(card)) continue;
                if (enhanced) player.Deck.PutOnTop(card);
                else player.Deck.PutUnder(card);
            }
            ctx.SteadyTempoPlayed.Clear();
            ctx.SteadyTempoReturns.Clear();
        }
    }
}
