using System;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>忠诚契约的专属标记，与普通指挥槽及神器指挥旗分别记账。</summary>
    public static class LoyaltyActions
    {
        public const string SourceId = "hero_skill_pool_002:07";
        public static bool IsBound(PlayerState player, UnitState unit) => unit != null &&
            ReferenceEquals(player.LoyaltyUnit, unit) && !unit.IsDestroyed && player.Units.Contains(unit);
        public static bool SlotAvailable(PlayerState player) => player.LoyaltySkill != null &&
            player.Skills.Any(s => ReferenceEquals(s, player.LoyaltySkill)) && !IsBound(player, player.LoyaltyUnit);
        public static void SelectRecruit(PlayerState player,ActionContext context,bool selected)
        {
            if(context==null||!context.InNegotiation||context.InteractionClosed||!RecruitmentService.BoundSiteIsCurrent(player,context))
                throw new InvalidOperationException("忠诚招募选择须在同一实际地点的正式交涉中");
            if(selected&&(!SlotAvailable(player)||context.CommandBannerForRecruit!=null||context.UnitToDisband!=null))
                throw new InvalidOperationException("须选择空闲本人忠诚槽且不混用旌旗/解散选择");
            context.UseLoyaltySlot=selected;
        }
        public static void ValidateRecruit(PlayerState player, ActionContext context)
        {
            if (context?.UseLoyaltySlot != true) return;
            if (!SlotAvailable(player) || context.CommandBannerForRecruit != null || context.UnitToDisband != null)
                throw new InvalidOperationException("必须选择空闲的忠诚专属槽，不能同时使用指挥旗或解散部队");
        }
        public static void BindRecruit(PlayerState player, ActionContext context, UnitState unit)
        {
            if (context?.UseLoyaltySlot != true) return;
            ValidateRecruit(player,context);
            if (unit == null || unit.IsDestroyed || !player.Units.Contains(unit)
                || !context.RecruitedUnitsThisTurn.Contains(unit))
                throw new InvalidOperationException("忠诚标记只能绑定本次真实招募的自有单位");
            player.LoyaltyUnit = unit;
            context.UseLoyaltySlot = false;
        }
        public static void ValidateDismiss(PlayerState player, UnitState unit)
        {
            if (IsBound(player, unit)) throw new InvalidOperationException("忠诚契约指挥的部队不能主动解散");
        }
    }
}
