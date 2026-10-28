// MageKnightLogicProj/Data/RangedResult.cs
using System.Collections.Generic;

namespace MK.Logic.Data
{
    /// <summary>远程/攻城阶段的结算结果</summary>
    public sealed record RangedResult(
        IReadOnlyList<int> KilledIndices,
        int FameGained,
        int ReputationGained = 0,
        IReadOnlyDictionary<int, int> AttackCrystals = null,
        int ExplosiveBoltArmorUses = 0);
}
