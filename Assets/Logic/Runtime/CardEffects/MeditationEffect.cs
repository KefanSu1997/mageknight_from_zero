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
    public sealed class MeditationEffect : ICardEffect
    {
        private readonly bool _enh;
        public MeditationEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var destTop = option == 0;
            var selected = new List<DeedCard>();
            if (_enh)
            {
                selected.AddRange(ctx.CardsFromDiscard.Take(2));
                foreach (var c in selected)
                    player.Deck.RemoveFromDiscard(c);
                ctx.CardsFromDiscard.Clear();
            }
            else
            {
                var disc = player.Deck.DiscardPile.ToList();
#if NETSTANDARD2_1
                var rnd = new Random();
#else
                var rnd = Random.Shared;
#endif
                for (int i = 0; i < 2 && disc.Count > 0; i++)
                {
                    int idx = rnd.Next(disc.Count);
                    var c = disc[idx];
                    selected.Add(c);
                    player.Deck.RemoveFromDiscard(c);
                    disc.RemoveAt(idx);
                }
            }
            foreach (var c in selected)
            {
                if (destTop) player.Deck.PutOnTop(c); else player.Deck.PutUnder(c);
            }
            ctx.NextDrawBonus += 2;
        }
    }
}
