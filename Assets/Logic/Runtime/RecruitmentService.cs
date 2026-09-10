namespace MK.Logic.Runtime
{
    using MK.Logic.Core;
    using MK.Logic.Data;

    /// <summary>
    /// 负责部队招募的核心逻辑。
    /// </summary>
    public sealed class RecruitmentService
    {
        public static int GetCost(PlayerState player, UnitCard candidate, RecruitLocation location,
            ActionContext? ctx = null, bool applyReputationModifier = true)
        {
            int modifier = applyReputationModifier ? ReputationTable.CalcRecruitModifier(player.Reputation) : 0;
            int cost = System.Math.Max(0, candidate.InfluenceCost + modifier - (ctx?.RecruitDiscount ?? 0));
            return cost + (location.HasFlag(RecruitLocation.RefugeeCamp) && candidate.IsElite ? 2 : 0);
        }
        /// <summary>
        /// 尝试在指定地点招募部队。若招募成功，将其加入玩家单位列表并返回 true。
        /// </summary>
        /// <param name="player">进行招募的玩家</param>
        /// <param name="candidate">待招募的部队卡</param>
        /// <param name="location">当前所在地点</param>
        /// <param name="influenceGenerated">本次可用影响力总值</param>
public bool TryRecruit(
            PlayerState    player,
            UnitCard       candidate,
            RecruitLocation location,
            int            influenceGenerated,
            ActionContext? ctx = null,
            IGameLogger?   logger = null,
            bool applyReputationModifier = true)
        {
            // 1️⃣ 地点验证：允许的地点必须落在枚举定义内，且非 None
            const RecruitLocation ValidMask =
                RecruitLocation.Village | RecruitLocation.Keep |
                RecruitLocation.Monastery | RecruitLocation.City |
                RecruitLocation.MageTower | RecruitLocation.RefugeeCamp;

            if (location == RecruitLocation.None || (location & ValidMask) == 0)
            {
                logger?.Log($"RecruitFail P{player.Id} invalidLocation");
                return false; // 非法地点直接拒绝
            }

            // 难民营视为拥有所有图标，因此不检查掩码
            if (!location.HasFlag(RecruitLocation.RefugeeCamp) &&
                (candidate.RecruitLocationMask & location) == 0)
            {
                logger?.Log($"RecruitFail P{player.Id} maskMismatch");
                return false; // 普通地点不在允许列表内
            }

            // 2️⃣ 计算最终招募费用，声望正负都会影响 Influence 消耗
            int discount = ctx?.RecruitDiscount ?? 0;
            int cost = GetCost(player, candidate, location, ctx, applyReputationModifier);

            if (influenceGenerated < cost)
            {
                logger?.Log($"RecruitFail P{player.Id} cost {cost}");
                return false; // 影响力不足
            }

            // 3️⃣ 检查指挥槽。当前简单处理：若满则招募失败
            if (player.FreeSlots <= 0)
            {
                logger?.Log($"RecruitFail P{player.Id} noSlot");
                return false;
            }

            // 4️⃣ 创建运行时状态并加入玩家
            var state = new UnitState(candidate);
            player.Units.Add(state);
            logger?.Log($"RecruitSuccess P{player.Id} {candidate.Id}");

            if (ctx != null)
                ctx.HasRecruited = true;

            if (discount > 0 && ctx != null && ctx.IntimidateUsed)
            {
                player.Reputation -= 1;
                ctx.IntimidateUsed = false;
                ctx.RecruitDiscount = 0;
            }

            if (ctx != null)
            {
                if (ctx.FamePerRecruit > 0)
                    player.Fame += ctx.FamePerRecruit;
                if (ctx.ReputationPerRecruit > 0)
                    player.Reputation += ctx.ReputationPerRecruit;
            }

            return true;
        }
    }
}
