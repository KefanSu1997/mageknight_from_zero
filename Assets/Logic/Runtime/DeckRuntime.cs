using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 遊戲執行時的簡易牌堆，支援洗牌、抽牌與回收棄牌。
    /// 可用於 Unity 端以 ScriptableObject 生成實際牌組。
    /// </summary>
    /// <typeparam name="T">牌的資料類型</typeparam>
    public sealed class DeckRuntime<T>
    {
        /// <summary>抽牌堆。</summary>
        public List<T> DrawPile { get; private set; }

        /// <summary>棄牌堆。</summary>
        private readonly Stack<T> _discard = new();

        /// <summary>檢視棄牌堆內容。</summary>
        public IReadOnlyCollection<T> DiscardPile => _discard.ToList();

        /// <summary>
        /// 建立空的牌堆。
        /// </summary>
        public DeckRuntime() => DrawPile = new List<T>();

        /// <summary>
        /// 以指定牌列表初始化牌堆。
        /// </summary>
        public DeckRuntime(IEnumerable<T> cards) => DrawPile = cards.ToList();

        /// <summary>
        /// 洗混抽牌堆。
        /// </summary>
        public void Shuffle()
        {
#if NETSTANDARD2_1
            var rnd = new System.Random();
#else
            var rnd = System.Random.Shared;
#endif
            DrawPile = DrawPile.OrderBy(_ => rnd.Next()).ToList();
        }

        /// <summary>
        /// 從抽牌堆抽取一張，若為空則自動回收棄牌堆再抽牌。
        /// </summary>
        public T Draw()
        {
            if (DrawPile.Count == 0)
                Recycle();
            var card = DrawPile[0];
            DrawPile.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// 將一張牌置入棄牌堆。
        /// </summary>
        public void Discard(T card) => _discard.Push(card);

        /// <summary>
        /// 將棄牌堆全部洗回抽牌堆。
        /// </summary>
        public void Recycle()
        {
            if (_discard.Count == 0) return;
            DrawPile = _discard.ToList();
            _discard.Clear();
            Shuffle();
        }
    }
}
