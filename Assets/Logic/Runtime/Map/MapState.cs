using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 全局地圖的狀態容器，包括兩個牌堆以及已放置的瓷磚。
    /// </summary>
    public sealed class MapState
    {
        public TileDeck Countryside { get; set; } = new();
        public TileDeck Core { get; set; } = new();
        public MapTile? CityTile { get; internal set; }

        public Dictionary<AxialCoord, MapTile> Placed { get; } = new();
    }
}
