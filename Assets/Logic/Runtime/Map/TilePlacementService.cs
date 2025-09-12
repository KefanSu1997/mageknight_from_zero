using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 負責驗證並放置地圖瓷磚的服務類。
    /// </summary>
    public sealed class TilePlacementService
    {
        /// <summary>
        /// 檢查瓷磚在指定座標與旋轉下是否能放置。
        /// 目前僅驗證：必須至少與一塊既有瓷磚相鄰，
        /// 且在城邦放出前，Core 周邊不可多於 Countryside。
        /// </summary>
        public bool CanPlace(MapState map, MapTile tile, AxialCoord coord, int rotation)
        {
            if (map.Placed.ContainsKey(coord)) return false;

            // 預覽旋轉後的結果，但不修改實體物件
            int steps = ((rotation % 6) + 6) % 6;

            int coreAdj = 0;
            int countryAdj = 0;
            bool hasNeighbor = false;

            for (int dir = 0; dir < 6; dir++)
            {
                var neighborCoord = coord + AxialCoord.NeighborDirs[dir];
                if (!map.Placed.TryGetValue(neighborCoord, out var neighbor))
                    continue;

                hasNeighbor = true;

                if (neighbor.Set == TileSet.Core) coreAdj++;
                else if (neighbor.Set == TileSet.Countryside) countryAdj++;
            }

            if (!hasNeighbor) return false;
            if (tile.Set == TileSet.Core && map.CityTile == null && coreAdj > countryAdj)
                return false;

            return true;
        }

        /// <summary>
        /// 在確認可放置的前提下實際放置瓷磚。
        /// </summary>
        public void Place(MapState map, MapTile tile, AxialCoord coord, int rotation)
        {
            if (!CanPlace(map, tile, coord, rotation))
                throw new System.InvalidOperationException("Tile cannot be placed.");

            // 實際旋轉並放置
            tile.Rotate(rotation);
            map.Placed[coord] = tile;
            if (tile.Set == TileSet.City)
                map.CityTile = tile;
        }
    }
}
