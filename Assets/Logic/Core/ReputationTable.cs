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
            // 正声望降低费用，负声望提高费用
            int adj = -reputation;
            if (adj > 3) adj = 3;
            if (adj < -3) adj = -3;
            return adj;
        }
    }
}
