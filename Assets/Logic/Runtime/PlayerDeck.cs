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
        private readonly List<DeedCard> _played = new();
        public IReadOnlyList<DeedCard> PlayedCards => _played;
        public int CardsPlayedOrDiscardedThisTurn { get; private set; }
        public bool EmptyHandExceptionAtTurnStart { get; private set; }
        internal void RecordCardPlayed() => CardsPlayedOrDiscardedThisTurn++;
        internal void BeginTurnCards()
        {
            CardsPlayedOrDiscardedThisTurn=0;
            EmptyHandExceptionAtTurnStart=Hand.Count==0 && DrawPileCount>0;
        }
        private readonly List<DeedCard> _removed = new();
        public IReadOnlyList<DeedCard> RemovedCards => _removed;
        private readonly List<DeedCard> _roundAside = new();
        public IReadOnlyList<DeedCard> RoundAsideCards => _roundAside;
        private readonly List<DeedCard> _tacticAside = new();
        public IReadOnlyList<DeedCard> TacticAsideCards => _tacticAside;

        internal void ReturnTacticAside()
        {
            foreach (var card in _tacticAside) _discard.Push(card);
            _tacticAside.Clear();
        }

        internal void StoreTopUnderTactic()
        {
            if (_draw.Count == 0) throw new System.InvalidOperationException("牌库为空，不能蓄存牌库顶牌");
            _tacticAside.Add(_draw.Pop());
        }

        internal void CollectTacticAside()
        {
            Hand.AddRange(_tacticAside);
            _tacticAside.Clear();
        }

        private static void Shuffle(List<DeedCard> cards, System.Random random)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); }
        }
        internal void ShuffleDraw(System.Random random)
        {
            var cards = _draw.ToList(); Shuffle(cards, random); _draw.Clear();
            foreach (var card in cards) _draw.Push(card);
        }
        internal void Rethink(IReadOnlyList<DeedCard> selected, System.Random random)
        {
            foreach (var card in selected) TryDiscardExact(card);
            DrawExact(selected.Count); // Replacement cards cannot be the just-discarded cards.
            while (_discard.Count > 0) _draw.Push(_discard.Pop());
            ShuffleDraw(random);
        }
        internal void MidnightMeditation(IReadOnlyList<DeedCard> selected, System.Random random)
        {
            foreach (var card in selected)
            {
                Hand.RemoveAt(Hand.FindIndex(c => ReferenceEquals(c, card)));
                _draw.Push(card);
            }
            ShuffleDraw(random); DrawExact(selected.Count);
        }
        internal void SearchDrawToHand(DeedCard selected, System.Random random)
        {
            var cards = _draw.ToList();
            int index = cards.FindIndex(c => ReferenceEquals(c, selected));
            if (index < 0) throw new System.InvalidOperationException("必须选择实际牌库中的一张牌");
            cards.RemoveAt(index); Hand.Add(selected); Shuffle(cards, random); _draw.Clear();
            foreach (var card in cards) _draw.Push(card);
        }
        internal void RefillLongNight(System.Random random)
        {
            if (_draw.Count != 0) throw new System.InvalidOperationException("漫漫长夜只能在牌库为空时使用");
            var cards = _discard.ToList(); Shuffle(cards, random); _discard.Clear();
            int count = System.Math.Min(3, cards.Count);
            foreach (var card in cards.Take(count)) _draw.Push(card);
            foreach (var card in cards.Skip(count)) _discard.Push(card);
        }

        internal void ResolveTimeBending(DeedCard source)
        {
            if (!TryTakePlayedExact(source))
                throw new System.InvalidOperationException("时间扭曲来源不在本人的已打出区");
            _roundAside.Add(source);
            ReturnTimeBendingPlayed();
        }

        internal void ReturnTimeBendingPlayed()
        {
            Hand.AddRange(_played);
            _played.Clear();
        }

        internal void ReturnRoundAside()
        {
            foreach (var card in _roundAside) _discard.Push(card);
            _roundAside.Clear();
        }

        public bool TryRemovePermanentlyExact(DeedCard card)
        {
            int index = Hand.FindIndex(c => ReferenceEquals(c, card));
            if (index < 0) return false;
            Hand.RemoveAt(index);
            _removed.Add(card);
            return true;
        }

        public bool TryPutInPlay(DeedCard card)
        {
            int index = Hand.FindIndex(c => ReferenceEquals(c, card));
            if (index < 0) return false;
            Hand.RemoveAt(index);
            _played.Add(card);
            RecordCardPlayed();
            return true;
        }

        /// <summary>只有明确从已打出区焚毁的规则使用，不放宽普通手牌去除入口。</summary>
        internal bool TryRemovePlayedPermanentlyExact(DeedCard card)
        {
            if (!TryTakePlayedExact(card)) return false;
            _removed.Add(card);
            return true;
        }

        internal bool TryTakePlayedExact(DeedCard card)
        {
            int index = _played.FindIndex(c => ReferenceEquals(c, card));
            if (index < 0) return false;
            _played.RemoveAt(index);
            return true;
        }

        public void DiscardPlayedCards()
        {
            foreach (var card in _played) _discard.Push(card);
            _played.Clear();
        }

        /// <summary>當前手牌列表。</summary>
        public List<DeedCard> Hand { get; } = new();
        /// <summary>实际新获得到手牌的创伤累计量；抽到已有伤牌不计入。</summary>
        public long HandWoundsReceived { get; private set; }
        internal event System.Action<DeedCard> NewHandWound;
        private readonly List<DeedCard> _woundsReceivedThisTurn = new();
        public int NewWoundsThisTurn => _woundsReceivedThisTurn.Count;

        public void GainWound(bool toDiscard = false)
        {
            var card = new DeedCard("w", CardType.Wound);
            _woundsReceivedThisTurn.Add(card);
            if (toDiscard) _discard.Push(card);
            else
            {
                Hand.Add(card);
                HandWoundsReceived++;
                NewHandWound?.Invoke(card);
            }
        }

        internal void BeginTurnWounds() => _woundsReceivedThisTurn.Clear();

        internal void EndTurnWounds(bool remove)
        {
            if (remove)
                foreach (var card in _woundsReceivedThisTurn)
                {
                    int index = Hand.FindIndex(c => ReferenceEquals(c,card));
                    bool found = index >= 0;
                    if (found) Hand.RemoveAt(index);
                    else found = TryRemoveFromDiscardExact(card) || TryTakePlayedExact(card) || TakeFromDrawExact(card);
                    if (found) _removed.Add(card);
                }
            _woundsReceivedThisTurn.Clear();
        }

        private bool TakeFromDrawExact(DeedCard card)
        {
            var cards = _draw.ToList();
            int index = cards.FindIndex(c => ReferenceEquals(c,card));
            if (index < 0) return false;
            cards.RemoveAt(index); _draw.Clear();
            foreach (var remaining in cards.AsEnumerable().Reverse()) _draw.Push(remaining);
            return true;
        }

        /// <summary>Compatibility writes update real wound cards; counts never live separately.</summary>
        internal void SetWoundCount(int count, bool inDiscard)
        {
            if (count < 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            var wounds = (inDiscard ? _discard.ToList() : Hand)
                .Where(card => card.Type == CardType.Wound).ToArray();
            foreach (var card in wounds.Take(System.Math.Max(0, wounds.Length - count)))
            {
                if (inDiscard) RemoveFromDiscard(card);
                else Hand.Remove(card);
            }
            for (int i = wounds.Length; i < count; i++)
            {
                GainWound(inDiscard);
            }
        }

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

        /// <summary>轮末将仍持有的原牌、伤牌及剩余牌库一起洗牌；不触及永久去除或附旗牌区。</summary>
        internal void ShuffleForNewRound(System.Random random = null)
        {
            foreach (var card in Hand) _discard.Push(card);
            Hand.Clear();
            while (_draw.Count > 0) _discard.Push(_draw.Pop());
            DiscardPlayedCards();
            ShuffleFromDiscard(random);
        }

        /// <summary>仅将弃牌堆洗入牌库；完整轮末回收使用ShuffleForNewRound。</summary>
        public void ShuffleFromDiscard(System.Random random = null)
        {
            if (_discard.Count == 0) return;
            var tmp = _discard.ToList();
#if NETSTANDARD2_1
            var rnd = random ?? new System.Random();
#else
            var rnd = random ?? System.Random.Shared;
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
            {
                _discard.Push(c);
                CardsPlayedOrDiscardedThisTurn++;
            }
        }

        /// <summary>按所选手牌实例弃牌，避免同ID卡牌的值相等替换了实际目标。</summary>
        public bool TryDiscardExact(DeedCard card)
        {
            int index = Hand.FindIndex(c => ReferenceEquals(c, card));
            if (index < 0) return false;
            Hand.RemoveAt(index);
            _discard.Push(card);
            CardsPlayedOrDiscardedThisTurn++;
            return true;
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

        /// <summary>移除指定弃牌实例；同ID牌不代替所选牌。</summary>
        public bool TryRemoveFromDiscardExact(DeedCard card)
        {
            var cards = _discard.ToList();
            int index = cards.FindIndex(c => ReferenceEquals(c, card));
            if (index < 0) return false;
            cards.RemoveAt(index);
            _discard.Clear();
            foreach (var c in cards.AsEnumerable().Reverse()) _discard.Push(c);
            return true;
        }

        /// <summary>將卡牌放回牌堆頂。</summary>
        public void PutOnTop(DeedCard c) => _draw.Push(c);

        /// <summary>將卡牌放到底牌堆。</summary>
        public void PutUnder(DeedCard c)
        {
            var tmp = _draw.ToList();
            tmp.Add(c);
            _draw.Clear();
            foreach (var d in tmp.AsEnumerable().Reverse())
                _draw.Push(d);
        }

        /// <summary>將牌堆設置為給定列表，常用於初始化。</summary>
        public void SetDeck(IEnumerable<DeedCard> cards)
        {
            _woundsReceivedThisTurn.Clear();
            _removed.Clear();
            _draw.Clear();
            foreach (var c in cards.Reverse())
                _draw.Push(c);
            _discard.Clear();
            Hand.Clear();
            _played.Clear();
        }

        /// <summary>牌堆剩餘張數，供邏輯判斷使用。</summary>
        public int DrawPileCount => _draw.Count;

        /// <summary>
        /// 取得目前牌堆的完整列表，僅用於日誌輸出與測試。
        /// </summary>
        public IReadOnlyList<DeedCard> DrawPile => _draw.ToList();
    }
}
