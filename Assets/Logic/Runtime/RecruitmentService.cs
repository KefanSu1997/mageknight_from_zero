namespace MK.Logic.Runtime
{
    using MK.Logic.Core;
    using MK.Logic.Data;

    /// <summary>
    /// 负责部队招募的核心逻辑。
    /// </summary>
    public sealed class RecruitmentService
    {
        public static void BeginInteraction(PlayerState player, ActionContext ctx, RecruitLocation location)
        {
            if (player.Reputation <= -7 && !HeroSocialSkillRules.ReputationEffectsIgnored(ctx) || ctx.InteractionReputationModifier.HasValue || ctx.InteractionClosed || ctx.InBattle || ctx.TurnActionCommitted || ctx.TemporalPortalTurn)
                throw new System.InvalidOperationException("本回合不能再次开始交涉");
            if (!ValidLocation(location) || location == RecruitLocation.MagicalGlade)
                throw new System.InvalidOperationException("交涉地点无效；魔法空地招募不属于交涉");
            int modifier = HeroSocialSkillRules.ReputationEffectsIgnored(ctx) ? 0 : -ReputationTable.CalcRecruitModifier(player.Reputation);
            ctx.InteractionReputationModifier = modifier;
            ctx.InteractionAppliedModifier = modifier;
            ctx.InteractionLocation = location;
            ctx.InNegotiation = ctx.TurnActionCommitted = true;
            ctx.InfluencePool += modifier;
        }

        public static void EndInteraction(ActionContext ctx)
        {
            ctx.InNegotiation = false;
            ctx.InteractionClosed = true;
            ctx.InfluencePool = 0;
        }

        private static bool ValidLocation(RecruitLocation location)
        {
            const RecruitLocation mask = RecruitLocation.Village | RecruitLocation.Keep | RecruitLocation.Monastery
                | RecruitLocation.City | RecruitLocation.MageTower | RecruitLocation.RefugeeCamp | RecruitLocation.MagicalGlade;
            int value = (int)location;
            return value > 0 && (value & (value - 1)) == 0 && (location & mask) != 0;
        }

        public static bool BoundSiteIsCurrent(PlayerState player, ActionContext ctx)
            => ctx?.InteractionSite != null && ctx.InteractionPosition.HasValue
                && player.Position.Equals(ctx.InteractionPosition.Value)
                && ctx.MovementMap?.Placed.ContainsKey(player.Position) == true
                && ctx.MovementMap.Places.TryGetValue(player.Position, out var site)
                && System.Object.ReferenceEquals(site, ctx.InteractionSite);

        public static bool AllowsWhiteCityRecruitment(PlayerState player, ActionContext ctx)
            => BoundSiteIsCurrent(player, ctx) && ctx.InNegotiation && !ctx.InteractionClosed
                && ctx.InteractionLocation == RecruitLocation.City
                && ctx.InteractionSite.Type == PlaceType.City && ctx.InteractionSite.Conquered
                && ctx.InteractionSite.CityColor == CityColor.White;

        private static int InteractionModifier(PlayerState player, UnitCard candidate, ActionContext? ctx)
        {
            int frozen = HeroSocialSkillRules.ReputationEffectsIgnored(ctx) ? 0 : ctx?.InteractionReputationModifier ?? -ReputationTable.CalcRecruitModifier(player.Reputation);
            if (candidate.ReverseRecruitReputation || ctx?.InteractionReversed == true) return -frozen;
            return candidate.DoubleRecruitReputation || ctx?.InteractionDoubled == true ? 2 * frozen : frozen;
        }

        public static int GetCost(PlayerState player, UnitCard candidate, RecruitLocation location,
            ActionContext? ctx = null, bool applyReputationModifier = true)
        {
            int adjustment = applyReputationModifier && location != RecruitLocation.MagicalGlade
                ? InteractionModifier(player, candidate, ctx) - (ctx?.InteractionAppliedModifier ?? 0) : 0;
            int surcharge = location == RecruitLocation.RefugeeCamp
                ? (candidate.RecruitLocationMask.HasFlag(RecruitLocation.Village) ? 0
                    : (candidate.RecruitLocationMask & (RecruitLocation.Keep | RecruitLocation.MageTower | RecruitLocation.Monastery)) != 0 ? 1 : 3) : 0;
            int cost = System.Math.Max(0, candidate.InfluenceCost + surcharge
                - (ctx?.RecruitDiscount ?? 0) + (ctx?.IntimidateUsed==true && !ctx.UseIntimidateRecruitDiscount?2:0)
                - (ctx?.CharmRecruitDiscount ?? 0) - (ctx?.UseLoyaltySlot == true && LoyaltyActions.SlotAvailable(player) ? 5 : 0));
            // The first successful special recruitment reconciles the entire interaction once.
            // Keep the delta outside the printed-price clamp: an earlier penalty can be refunded.
            return cost - adjustment;
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
            bool applyReputationModifier = true,
            ManaColor? familiarMana = null)
        {
            if (ctx?.ForbidInfluenceRecruitment == true) return false;
            if (applyReputationModifier && ctx?.InteractionSite != null && !BoundSiteIsCurrent(player, ctx)) return false;
            if (ctx?.UseLoyaltySlot == true && (!LoyaltyActions.SlotAvailable(player) || ctx.CommandBannerForRecruit != null || ctx.UnitToDisband != null)) return false;
            // 1️⃣ 地点验证：允许的地点必须落在枚举定义内，且非 None
            if (!ValidLocation(location))
            {
                logger?.Log($"RecruitFail P{player.Id} invalidLocation");
                return false; // 非法地点直接拒绝
            }

            bool glade = location == RecruitLocation.MagicalGlade;
            if (glade && (!candidate.RequiresAttunement || ctx == null || ctx.InBattle || ctx.TemporalPortalTurn
                || ctx.InteractionReputationModifier.HasValue || ctx.InteractionClosed
                || ctx.TurnActionCommitted && !ctx.FamiliarRecruitmentAtGlade)) return false;
            if (glade) applyReputationModifier = false;
            if (applyReputationModifier && (ctx?.InBattle == true || ctx?.TemporalPortalTurn == true
                || ctx?.InteractionClosed == true || (ctx?.InteractionReputationModifier.HasValue == true
                    && ctx.InteractionLocation != location)
                || (ctx?.InteractionReputationModifier.HasValue != true && (player.Reputation <= -7 && !HeroSocialSkillRules.ReputationEffectsIgnored(ctx) || ctx?.TurnActionCommitted == true)))) return false;

            // The original Heroes/Thugs marks are mutually exclusive for the whole interaction.
            // Reject before spending or changing its frozen reputation adjustment.
            if (applyReputationModifier &&
                (candidate.DoubleRecruitReputation && ctx?.InteractionReversed == true
                 || candidate.ReverseRecruitReputation && ctx?.InteractionDoubled == true)) return false;

            // 难民营视为拥有所有图标，因此不检查掩码
            if (!location.HasFlag(RecruitLocation.RefugeeCamp)
                && !(location == RecruitLocation.City && AllowsWhiteCityRecruitment(player, ctx))
                && (candidate.RecruitLocationMask & location) == 0)
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
            bool useCommand = ctx != null && BannerActions.CommandAvailable(player,ctx,ctx.CommandBannerForRecruit);
            if ((ctx?.CommandBannerForRecruit != null && !useCommand) || player.OrdinaryFreeSlots < 0 || (player.OrdinaryFreeSlots == 0 && !useCommand && ctx?.UseLoyaltySlot != true))
            {
                logger?.Log($"RecruitFail P{player.Id} noSlot");
                return false;
            }

            // Additional mana is paid only after every location, influence and command-slot check.
            if (candidate.RequiresAttunement && (ctx == null || !familiarMana.HasValue
                || !FamiliarActions.IsBasicColor(familiarMana.Value)
                || !player.Mana.TryPayExactColors(new[] { familiarMana.Value }, ctx, player))) return false;
            // 4️⃣ 创建运行时状态并加入玩家
            var state = new UnitState(candidate);
            if (candidate.RequiresAttunement) state.AttunementColor = familiarMana;
            player.Units.Add(state);
            if (glade) { ctx.FamiliarRecruitmentAtGlade = true; ctx.TurnActionCommitted = true; }
            if (ctx != null) BannerActions.RecordRecruit(player,ctx,state);
            LoyaltyActions.BindRecruit(player,ctx,state);
            if (applyReputationModifier && ctx != null)
            {
                ctx.InteractionReputationModifier ??= HeroSocialSkillRules.ReputationEffectsIgnored(ctx) ? 0 : -ReputationTable.CalcRecruitModifier(player.Reputation);
                ctx.InteractionAppliedModifier = InteractionModifier(player, candidate, ctx);
                ctx.InteractionReversed |= candidate.ReverseRecruitReputation;
                ctx.InteractionDoubled |= candidate.DoubleRecruitReputation;
                ctx.InteractionLocation = location;
                ctx.InNegotiation = true;
            }
            logger?.Log($"RecruitSuccess P{player.Id} {candidate.Id}");

            if (ctx != null)
            {
                ctx.HasRecruited = true;
                ctx.InfluenceRecruitmentOccurred = true;
            }

            if (discount > 0 && ctx != null && ctx.IntimidateUsed && ctx.UseIntimidateRecruitDiscount)
            {
                player.Reputation -= 1;
                ctx.IntimidateUsed = false;
                ctx.RecruitDiscount = System.Math.Max(0,ctx.RecruitDiscount-2);
            }

            if (ctx != null)
            {
                ctx.CharmRecruitDiscount = 0;
                if (ctx.FamePerRecruit > 0)
                    player.Fame += ctx.FamePerRecruit;
                if (ctx.ReputationPerRecruit > 0)
                    player.Reputation += ctx.ReputationPerRecruit;
            }

            return true;
        }
    }
}
