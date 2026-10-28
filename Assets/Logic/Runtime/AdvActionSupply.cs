using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 高级行动牌面供固定三张（MKUE规则书p.3）；不因揭示核心地块而扩大。
    /// </summary>
    public sealed class AdvActionSupply
    {
        private readonly Deck<AdvActionCard> _deck = new();
        /// <summary>當前面供的卡牌列表，索引 0 為最底端。</summary>
        public readonly List<AdvActionCard> Offer = new();
        /// <summary>历次轮换回底的历史记录，非独立牌区；实体已回到备用牌库。</summary>
        public readonly List<AdvActionCard> DiscardPile = new();

        /// <summary>設定牌堆內容，通常於初始化或測試時使用。</summary>
        public void SetDeck(IEnumerable<AdvActionCard> cards) => _deck.Set(cards);

        /// <summary>
        /// 每轮开始将最底一张放回备用牌库底并补至三张；保留参数兼容旧调用方。
        /// </summary>
        public void Refill(bool coreRevealed)
        {
            // 只轮换最底牌，保留其余供给的实际对象与顺序。
            if (Offer.Count > 0)
            {
                DiscardPile.Add(Offer[0]);
                _deck.PutUnder(Offer[0]);
                Offer.RemoveAt(0); // 其餘卡牌索引自動向下移動
            }

            FillVacancies();
        }

        /// <summary>取得供给牌后立即补空位，保留未选牌的实例与顺序，不轮换。</summary>
        public void FillVacancies()
        {
            while (Offer.Count < 3 && _deck.TryDraw(out var c)) Offer.Add(c);
        }

        /// <summary>玩家選取指定索引的牌。</summary>
        public AdvActionCard Take(int index)
        {
            var c = Offer[index];
            Offer.RemoveAt(index);
            FillVacancies();
            return c;
        }

        /// <summary>直接從牌堆頂抽一張牌。</summary>
        public AdvActionCard Draw() => _deck.Draw();

        /// <summary>嘗試抽牌，若牌堆為空則回傳 false。</summary>
        public bool TryDraw(out AdvActionCard card) => _deck.TryDraw(out card);
        public void PutUnder(AdvActionCard card) => _deck.PutUnder(card);
    }
}
