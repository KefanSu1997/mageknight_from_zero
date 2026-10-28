using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 法术面供最多三张；领取立即补位，新轮只将最底牌放回备用牌库底。
    /// </summary>
    public sealed class SpellSupply
    {
        private readonly Deck<SpellCard> _deck = new();
        /// <summary>面供法術列表，索引 0 為最底。</summary>
        public readonly List<SpellCard> Offer = new();
        /// <summary>历次轮换回底的历史记录，非独立牌区。</summary>
        public readonly List<SpellCard> DiscardPile = new();

        /// <summary>設定牌堆內容。</summary>
        public void SetDeck(IEnumerable<SpellCard> cards) => _deck.Set(cards);

        /// <summary>新轮仅最底牌回到备用牌库底，再补满三张。</summary>
        public void Refill()
        {
            // 历史记录不持有独立牌；实际对象继续在备用牌库中循环。
            if (Offer.Count > 0)
            {
                DiscardPile.Add(Offer[0]);
                _deck.PutUnder(Offer[0]);
                Offer.RemoveAt(0); // 其餘自動下移
            }

            FillVacancies();
        }

        /// <summary>领取奖励后的补位不轮换或丢弃仍未选择的法术。</summary>
        public void FillVacancies()
        {
            while (Offer.Count < 3 && _deck.TryDraw(out var c))
                Offer.Add(c);
        }

        /// <summary>玩家選取法術。</summary>
        public SpellCard Claim(int idx)
        {
            var c = Offer[idx];
            Offer.RemoveAt(idx);
            FillVacancies();
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
