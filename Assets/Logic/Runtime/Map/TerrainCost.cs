using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 根據地形與日夜狀態計算移動力消耗。
    /// </summary>
    public static class TerrainCost
    {
        public static int GetCost(TerrainType t, DayPart dp)
            => (t, dp) switch
            {
                // Forest terrain
                (TerrainType.Forest, DayPart.Day)   => 3,
                (TerrainType.Forest, DayPart.Night) => 2,
                
                // Desert terrain
                (TerrainType.Desert, DayPart.Day)   => 2,
                (TerrainType.Desert, DayPart.Night) => 3,
                
                // Mountain terrain
                (TerrainType.Mountain, DayPart.Day)   => 3,
                (TerrainType.Mountain, DayPart.Night) => 4,
                
                // Swamp terrain
                (TerrainType.Swamp, DayPart.Day)   => 3,
                (TerrainType.Swamp, DayPart.Night) => 5,
                
                // Hills terrain
                (TerrainType.Hills, DayPart.Day)   => 2,
                (TerrainType.Hills, DayPart.Night) => 3,
                
                // Plains terrain
                (TerrainType.Plains, _)            => 2,
                
                // Water/impassable terrain
                (TerrainType.Lake, _)              => int.MaxValue,
                (TerrainType.River, _)             => int.MaxValue,
                
                // Settlement terrain
                (TerrainType.Village, _)           => 1,
                (TerrainType.City, _)              => 1,
                (TerrainType.Keep, _)              => 1,
                
                // Ruins terrain
                (TerrainType.Ruins, _)             => 2,
                
                _                                   => 2
            };

        /// <summary>
        /// 检查地形是否可以在当前时间通过
        /// </summary>
        public static bool IsPassable(TerrainType t, DayPart dp)
        {
            int cost = GetCost(t, dp);
            return cost < int.MaxValue;
        }
    }
}
