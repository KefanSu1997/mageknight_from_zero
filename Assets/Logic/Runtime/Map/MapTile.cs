using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 一塊地圖瓷磚的數據結構。為簡化示例僅紀錄邊緣地形與牌堆歸屬。
    /// 旋轉時僅需改變 <see cref="Edges"/> 的順序。
    /// </summary>
    public sealed class MapTile
    {
        public TileSet Set { get; init; }
        public int Id { get; init; }
        public TerrainType[] Edges { get; private set; }
        public int Rotation { get; set; } = 0;

        public MapTile(TileSet set, int id, TerrainType[] edges)
        {
            Set = set;
            Id = id;
            Edges = edges;
        }

        // Compatibilty constructor for tests
        public MapTile()
        {
            Set = TileSet.Core;
            Id = 0;
            Edges = new TerrainType[6];
        }

        /// <summary>逆時針旋轉指定次數。</summary>
        public void Rotate(int steps)
        {
            steps = ((steps % 6) + 6) % 6;
            if (steps == 0) return;
            var rotated = new TerrainType[6];
            for (int i = 0; i < 6; i++)
                rotated[i] = Edges[(i + steps) % 6];
            Edges = rotated;
        }
    }
}
