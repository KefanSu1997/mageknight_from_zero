namespace MK.Logic.Core
{
    /// <summary>
    /// 根据玩家当前声望值提供招募费用修正。
    /// 规则：声望越高，招募越便宜；最低不会超过 ±3。
    /// </summary>
    public static class ReputationTable
    {
        /// <summary>
        /// 计算招募费用应加上的修正值（可能为负）。
        /// </summary>
        /// <param name="reputation">玩家当前声望</param>
        public static int CalcRecruitModifier(int reputation)
        {
            if (reputation <= -7)
                throw new System.InvalidOperationException("声誉位于X，不能交涉");
            // 声誉是从起始格移动的格数，不是格上印刷的影响力修正。
            return reputation < 0 ? (-reputation + 1) / 2 : -System.Math.Min(3, reputation / 2);
        }
    }
}
