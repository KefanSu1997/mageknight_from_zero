using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冥想 / 神遊：從棄牌堆取回兩張牌置頂或置底，並於下次抽牌提升上限。
    /// option 0 = 置頂，1 = 置底。
    /// 強效由 <see cref="ActionContext.CardsFromDiscard"/> 指定牌張。
    /// </summary>
    public sealed class MeditationEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public MeditationEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 && option != 1) throw new InvalidOperationException("冥想必须选择牌库顶或牌库底");
            if (!_enh) return;
            var discard = player.Deck.DiscardPile;
            if (ctx.CardsFromDiscard.Count != Math.Min(2, discard.Count))
                throw new InvalidOperationException("神游必须选择两张弃牌，不足时选择全部弃牌");
            for (int i = 0; i < ctx.CardsFromDiscard.Count; i++)
            {
                var card = ctx.CardsFromDiscard[i];
                if (!discard.Any(c => ReferenceEquals(c, card))
                    || ctx.CardsFromDiscard.Take(i).Any(c => ReferenceEquals(c, card)))
                    throw new InvalidOperationException("神游必须选择不同的实际弃牌实例");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var destTop = option == 0;
            var selected = new List<DeedCard>();
            if (_enh)
            {
                selected.AddRange(ctx.CardsFromDiscard);
            }
            else
            {
                var disc = player.Deck.DiscardPile.ToList();
#if NETSTANDARD2_1
                var rnd = ctx.EffectRandom ?? new Random();
#else
                var rnd = ctx.EffectRandom ?? Random.Shared;
#endif
                for (int i = 0; i < 2 && disc.Count > 0; i++)
                {
                    int idx = rnd.Next(disc.Count);
                    var c = disc[idx];
                    selected.Add(c);
                    disc.RemoveAt(idx);
                }
            }
            foreach (var c in selected) player.Deck.TryRemoveFromDiscardExact(c);
            ctx.CardsFromDiscard.Clear();
            // Selection order is the resulting top-to-bottom order in either destination.
            foreach (var c in destTop ? selected.AsEnumerable().Reverse() : selected)
            {
                if (destTop) player.Deck.PutOnTop(c); else player.Deck.PutUnder(c);
            }
            ctx.NextDrawBonus += 2;
        }
    }
}
