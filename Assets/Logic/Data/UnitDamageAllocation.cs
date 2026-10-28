using System.Collections.Generic;
using MK.Logic.Runtime;

namespace MK.Logic.Data
{
    /// <summary>按选择顺序用实际自有部队承担一个敌人的伤害，剩余伤害交给英雄。</summary>
    public sealed record UnitDamageAllocation(int TargetIndex, IReadOnlyList<UnitState> Units, int AttackIndex = 0);
}
