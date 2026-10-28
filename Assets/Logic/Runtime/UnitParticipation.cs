using System;

namespace MK.Logic.Runtime
{
    /// <summary>Site participation never replaces readiness, wounds, ownership or payment checks.</summary>
    public static class UnitParticipation
    {
        public static bool CanUse(PlayerState player, ActionContext context, UnitState unit)
            => context == null || !context.InBattle || context.UnitsAllowed || LoyaltyActions.IsBound(player, unit);

        public static void Require(PlayerState player, ActionContext context, UnitState unit)
        {
            if (!CanUse(player, context, unit))
                throw new InvalidOperationException("此地点禁止使用部队；只有忠诚契约实际指挥的部队例外");
        }
    }
}
