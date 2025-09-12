using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 神器牌堆，僅透過特殊行為獲得：翻兩選一，其餘置底。
    /// </summary>
    public sealed class ArtifactSupply
    {
        private readonly Deck<ArtifactCard> _deck = new();

        /// <summary>設定牌堆順序。</summary>
        public void SetDeck(IEnumerable<ArtifactCard> cards) => _deck.Set(cards);

        /// <summary>從牌堆頂翻開兩張。</summary>
        public (ArtifactCard a, ArtifactCard b) DrawTwo()
            => (_deck.Draw(), _deck.Draw());

        /// <summary>抽取頂部一張神器。</summary>
        public ArtifactCard Draw() => _deck.Draw();

        /// <summary>留下其中一張，另一張放至牌底。</summary>
        public void KeepOneReturn(ArtifactCard keep, ArtifactCard discard)
            => _deck.PutUnder(discard);

        /// <summary>放置到底部。</summary>
        public void PutUnder(ArtifactCard card) => _deck.PutUnder(card);

        /// <summary>剩餘牌數。</summary>
        public int Count => _deck.Count;
    }
}
