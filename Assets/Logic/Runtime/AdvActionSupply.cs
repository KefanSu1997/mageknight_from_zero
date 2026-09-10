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
        /// <summary>被刷新時丟棄的卡牌，僅為記錄用途。</summary>
        public readonly List<AdvActionCard> DiscardPile = new();

        /// <summary>設定牌堆內容，通常於初始化或測試時使用。</summary>
        public void SetDeck(IEnumerable<AdvActionCard> cards) => _deck.Set(cards);

        /// <summary>
        /// 每轮开始弃置最底一张并补至三张；保留参数兼容旧调用方。
        /// </summary>
        public void Refill(bool coreRevealed)
        {
            // 每次刷新時先丟棄最底端的卡牌，並將其放入棄牌堆
            if (Offer.Count > 0)
            {
                DiscardPile.Add(Offer[0]);
                Offer.RemoveAt(0); // 其餘卡牌索引自動向下移動
            }

            const int target = 3;

            // 抽牌補滿至目標數量，抽到的新牌會放在最上端（列表尾端）
            while (Offer.Count < target && _deck.TryDraw(out var c))
                Offer.Add(c);
        }

        /// <summary>玩家選取指定索引的牌。</summary>
        public AdvActionCard Take(int index)
        {
            var c = Offer[index];
            Offer.RemoveAt(index);
            return c;
        }

        /// <summary>直接從牌堆頂抽一張牌。</summary>
        public AdvActionCard Draw() => _deck.Draw();

        /// <summary>嘗試抽牌，若牌堆為空則回傳 false。</summary>
        public bool TryDraw(out AdvActionCard card) => _deck.TryDraw(out card);
    }
}
