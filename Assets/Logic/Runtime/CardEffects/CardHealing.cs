using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Remove actual hand wounds, then resolve active draw-per-heal effects.</summary>
    public static class CardHealing
    {
        /// <summary>Spend saved healing on actual hand wounds or one owned unit; unit cost is its level per wound.</summary>
        public static void Spend(PlayerState player, ActionContext context, int wounds = 1, UnitState? unit = null, bool grailFirst = true)
        {
            if (context.InBattle) throw new InvalidOperationException("战斗中不能使用治疗效果");
            if (wounds <= 0) throw new InvalidOperationException("必须选择正数伤牌");
            if (unit != null && (!player.Units.Contains(unit) || unit.IsDestroyed))
                throw new InvalidOperationException("只能治疗未销毁的自有部队");
            int availableWounds = unit == null ? player.Wounds : unit.Wounds;
            if (wounds > availableWounds) throw new InvalidOperationException("目标没有足够伤牌");
            int cost = checked(wounds * (unit == null ? 1 : unit.Card.Level));
            if (cost <= 0 || cost > context.Healing.Total) throw new InvalidOperationException("治疗点不足或消费数量无效");
            player.Fame += context.Healing.Spend(cost, grailFirst);
            if (unit == null) Heal(player, context, wounds);
            else
            {
                HealUnit(player,context,unit,wounds);
            }
        }

        public static int HealUnit(PlayerState player, ActionContext context, UnitState unit, int amount)
        {
            if (context.InBattle) throw new InvalidOperationException("战斗中不能使用治疗效果");
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || amount <= 0)
                throw new InvalidOperationException("只能治疗未销毁的自有部队，数量必须为正");
            int healed = unit.Heal(amount);
            if (healed > 0)
            {
                context.HealedUnitsThisTurn.Add(unit);
                if (context.ReadyUnitOnHeal) unit.Ready();
            }
            return healed;
        }

        public static int Heal(PlayerState player, ActionContext context, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int healed = Math.Min(amount, player.Wounds);
            player.Wounds -= healed;
            context.HealedHandWoundsThisTurn += healed;
            if (context.DrawPerHeal > 0 && healed > 0)
                player.Deck.DrawExact(healed * context.DrawPerHeal);
            return healed;
        }
    }
}
