using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Remove actual hand wounds, then resolve active draw-per-heal effects.</summary>
    public static class CardHealing
    {
        public static int Heal(PlayerState player, ActionContext context, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int healed = Math.Min(amount, player.Wounds);
            player.Wounds -= healed;
            if (context.DrawPerHeal > 0 && healed > 0)
                player.Deck.DrawExact(healed * context.DrawPerHeal);
            return healed;
        }
    }
}
