// MageKnightLogicProj/Data/RangedResult.cs
using System.Collections.Generic;

namespace MK.Logic.Data
{
    /// <summary>远程/攻城阶段的结算结果</summary>
    public sealed record RangedResult(
        IReadOnlyList<int> KilledIndices,
        int FameGained);
}
