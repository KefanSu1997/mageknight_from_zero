using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 法術牌供給，每輪維持三張面供，回合結束時未選牌棄至底部。
    /// </summary>
    public sealed class SpellSupply
    {
        private readonly Deck<SpellCard> _deck = new();
        /// <summary>面供法術列表，索引 0 為最底。</summary>
        public readonly List<SpellCard> Offer = new();
        /// <summary>刷新時丟棄的法術，用於記錄牌序。</summary>
        public readonly List<SpellCard> DiscardPile = new();

        /// <summary>設定牌堆內容。</summary>
        public void SetDeck(IEnumerable<SpellCard> cards) => _deck.Set(cards);

        /// <summary>依序補滿三張。</summary>
        public void Refill()
        {
            // 先丟棄最底的法術，記錄至棄牌堆
            if (Offer.Count > 0)
            {
                DiscardPile.Add(Offer[0]);
                Offer.RemoveAt(0); // 其餘自動下移
            }

            // 一律維持三張面供，抽到的新牌放在最上端
            while (Offer.Count < 3 && _deck.TryDraw(out var c))
                Offer.Add(c);
        }

        /// <summary>玩家選取法術。</summary>
        public SpellCard Claim(int idx)
        {
            var c = Offer[idx];
            Offer.RemoveAt(idx);
            return c;
        }

        /// <summary>清空未選法術並全部放回牌底。</summary>
        public void DiscardUnchosen()
        {
            foreach (var c in Offer)
                _deck.PutUnder(c);
            Offer.Clear();
        }
    }
}
