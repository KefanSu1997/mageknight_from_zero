using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 提供依照卡牌 ID 查詢其顏色的輔助類。
    /// </summary>
    public static class CardColorHelper
    {
        private static readonly Dictionary<string, ManaColor> _map;

        static CardColorHelper()
        {
            _map = new();
            foreach (var c in CardJsonLoader.LoadBasicActions())
            {
                if (c.RequiredCrystals.Length > 0)
                    _map[c.Id] = c.RequiredCrystals[0];
            }
            foreach (var c in CardJsonLoader.LoadAdvancedActions())
            {
                if (c.RequiredCrystals.Length > 0)
                    _map[c.Id] = c.RequiredCrystals[0];
            }
            foreach (var s in CardJsonLoader.LoadSpells())
            {
                _map[s.Id] = s.ManaColor;
            }
        }

        /// <summary>
        /// 取得指定卡牌的顏色；若無資料則回傳 <c>null</c>。
        /// </summary>
        public static ManaColor? Get(string id)
        {
            return _map.TryGetValue(id, out var c) ? c : null;
        }
    }
}
