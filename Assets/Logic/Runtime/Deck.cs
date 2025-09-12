using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 泛型牌堆結構，支援抽牌與放到底牌堆。
    /// 用於各種公共資源堆疊。
    /// </summary>
    public sealed class Deck<T>
    {
        private readonly Queue<T> _cards = new();

        /// <summary>設定牌堆順序，可在測試中注入固定序列。</summary>
        public void Set(IEnumerable<T> cards)
        {
            _cards.Clear();
            foreach (var c in cards)
                _cards.Enqueue(c);
        }

        /// <summary>嘗試從頂部抽一張牌。</summary>
        public bool TryDraw(out T card)
        {
            if (_cards.Count > 0)
            {
                card = _cards.Dequeue();
                return true;
            }
            card = default!;
            return false;
        }

        /// <summary>強制抽牌，若空則擲出例外。</summary>
        public T Draw()
        {
            if (!TryDraw(out var c))
                throw new System.InvalidOperationException("Deck empty");
            return c;
        }

        /// <summary>放置到底部。</summary>
        public void PutUnder(T card) => _cards.Enqueue(card);

        /// <summary>牌堆中剩餘的張數。</summary>
        public int Count => _cards.Count;
    }
}
