using System.Collections.Generic;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 瓷磚牌堆的封裝。由 Stack 控制抽取順序。
    /// </summary>
    public sealed class TileDeck
    {
        private readonly Stack<MapTile> _stack = new();

        public int Count => _stack.Count;

        public void Push(MapTile tile) => _stack.Push(tile);

        public MapTile Draw() => _stack.Pop();

        // Compatibility method for tests
        public void SetCards(IEnumerable<MapTile> cards)
        {
            _stack.Clear();
            foreach (var card in cards)
                _stack.Push(card);
        }
    }
}
