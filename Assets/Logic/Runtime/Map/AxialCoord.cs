namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 六边格的轴向坐标表示，q 對應水平方向，r 對應斜向。
    /// 由於為值類型，用於字典鍵時需實作 Equals 與 GetHashCode。
    /// </summary>
    public readonly struct AxialCoord
    {
        public int Q { get; init; }
        public int R { get; init; }

        public AxialCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public void Deconstruct(out int q, out int r)
        {
            q = Q;
            r = R;
        }

        public override bool Equals(object? obj) =>
            obj is AxialCoord other && Q == other.Q && R == other.R;

        public override int GetHashCode() => System.HashCode.Combine(Q, R);

        public static AxialCoord operator +(AxialCoord a, AxialCoord b) =>
            new(a.Q + b.Q, a.R + b.R);

        /// <summary>六個相鄰方向的座標偏移。</summary>
        public static readonly AxialCoord[] NeighborDirs = new[]
        {
            new AxialCoord(1, 0),
            new AxialCoord(1, -1),
            new AxialCoord(0, -1),
            new AxialCoord(-1, 0),
            new AxialCoord(-1, 1),
            new AxialCoord(0, 1)
        };
    }
}
