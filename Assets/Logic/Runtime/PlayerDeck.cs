using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 管理玩家行動牌牌庫與手牌的資料結構。
    /// </summary>
    public sealed class PlayerDeck
    {
        private readonly Stack<DeedCard> _draw = new();
        private readonly Stack<DeedCard> _discard = new();

        /// <summary>當前手牌列表。</summary>
        public List<DeedCard> Hand { get; } = new();

        /// <summary>
        /// 根據日夜、聲望、戰術牌以及地形/技能修正計算手牌上限。
        /// </summary>
        public int HandLimit(
            DayPart dp,
            int repStep,
            int tacticBonus,
            bool keepPenalty,
            int extraBonus)
            => System.Math.Max(
                0,
                5 + repStep + tacticBonus + extraBonus
                  + (dp == DayPart.Night ? -1 : 0)
                  + (keepPenalty ? -1 : 0));

        /// <summary>
        /// 依照上限摸牌，若牌堆用盡則立刻停止，不會在本輪重新洗牌。
        /// </summary>
        public void DrawToLimit(int limit)
        {
            while (Hand.Count < limit && _draw.Count > 0)
                Hand.Add(_draw.Pop());
        }

        /// <summary>
        /// 抽取指定張數的牌，返回實際抽到的張數。
        /// 若牌堆耗盡，剩餘的抽牌要求會被忽略。
        /// </summary>
        public int DrawExact(int count)
        {
            int drawn = 0;
            while (drawn < count && _draw.Count > 0)
            {
                Hand.Add(_draw.Pop());
                drawn++;
            }
            return drawn;
        }

        /// <summary>
        /// 將棄牌堆重新洗入牌堆，用於新一輪開始時。
        /// </summary>
        public void ShuffleFromDiscard()
        {
            if (_discard.Count == 0) return;
            var tmp = _discard.ToList();
#if NETSTANDARD2_1
            var rnd = new System.Random();
#else
            var rnd = System.Random.Shared;
#endif
            for (int i = tmp.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (tmp[i], tmp[j]) = (tmp[j], tmp[i]);
            }
            foreach (var c in tmp)
                _draw.Push(c);
            _discard.Clear();
        }

        /// <summary>
        /// 將指定手牌丟入棄牌堆。
        /// </summary>
        public void Discard(DeedCard c)
        {
            if (Hand.Remove(c))
                _discard.Push(c);
        }

        /// <summary>
        /// 直接將一張卡牌置入棄牌堆，常用於從市場獲得新牌。
        /// </summary>
        public void GainToDiscard(DeedCard c) => _discard.Push(c);

        /// <summary>僅從手牌移除，不進入棄牌堆。</summary>
        public bool RemoveFromHand(DeedCard c) => Hand.Remove(c);

        /// <summary>檢視棄牌堆內容。</summary>
        public IReadOnlyList<DeedCard> DiscardPile => _discard.ToList();

        /// <summary>從棄牌堆移除指定卡牌。</summary>
        public bool RemoveFromDiscard(DeedCard c)
        {
            var list = _discard.ToList();
            if (!list.Remove(c)) return false;
            _discard.Clear();
            foreach (var d in list.AsEnumerable().Reverse())
                _discard.Push(d);
            return true;
        }

        /// <summary>將卡牌放回牌堆頂。</summary>
        public void PutOnTop(DeedCard c) => _draw.Push(c);

        /// <summary>將卡牌放到底牌堆。</summary>
        public void PutUnder(DeedCard c)
        {
            var tmp = _draw.Reverse().ToList();
            tmp.Add(c);
            _draw.Clear();
            foreach (var d in tmp.AsEnumerable().Reverse())
                _draw.Push(d);
        }

        /// <summary>將牌堆設置為給定列表，常用於初始化。</summary>
        public void SetDeck(IEnumerable<DeedCard> cards)
        {
            _draw.Clear();
            foreach (var c in cards.Reverse())
                _draw.Push(c);
            _discard.Clear();
            Hand.Clear();
        }

        /// <summary>牌堆剩餘張數，供邏輯判斷使用。</summary>
        public int DrawPileCount => _draw.Count;

        /// <summary>
        /// 取得目前牌堆的完整列表，僅用於日誌輸出與測試。
        /// </summary>
        public IReadOnlyList<DeedCard> DrawPile => _draw.ToList();
    }
}
