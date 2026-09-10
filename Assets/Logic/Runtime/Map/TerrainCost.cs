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
                (TerrainType.Forest, DayPart.Night) => 5,
                
                // Desert terrain
                (TerrainType.Desert, DayPart.Day)   => 5,
                (TerrainType.Desert, DayPart.Night) => 3,
                
                // Mountain terrain
                (TerrainType.Mountain, _) => int.MaxValue,
                
                // Swamp terrain
                (TerrainType.Swamp, _) => 5,
                
                // Hills terrain
                (TerrainType.Hills, _) => 3,
                (TerrainType.Wasteland, _) => 4,
                
                // Plains terrain
                (TerrainType.Plains, _)            => 2,
                
                // Water/impassable terrain
                (TerrainType.Lake, _)              => int.MaxValue,
                (TerrainType.River, _)             => int.MaxValue,
                
                // 城市固定2；旧数据把地点当作地形，村庄/要塞在演示中按平原承载。
                // 正式地图应把地点与底层地形分别存储。
                (TerrainType.Village, _)           => 2,
                (TerrainType.City, _)              => 2,
                (TerrainType.Keep, _)              => 2,
                
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
