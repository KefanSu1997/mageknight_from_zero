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

        /// <summary>原七格板块身份与印刷朝向；Placed 仅为既有移动规则提供逐格投影。</summary>
        public Dictionary<string, OriginalMapTile> OriginalTiles { get; } = new();
        public Dictionary<AxialCoord, OriginalMapCell> OriginalCells { get; } = new();

        public Dictionary<AxialCoord, MapTile> Placed { get; } = new();

        /// <summary>已揭示格的地点状态，沿用地点交互及驻守敌人的正式数据。</summary>
        public Dictionary<AxialCoord, PlaceState> Places { get; } = new();

        /// <summary>已经翻开的实际地点实体；移动或回合结束不会重新盖回去。</summary>
        public HashSet<PlaceState> RevealedSites { get; } = new();
    }
}
