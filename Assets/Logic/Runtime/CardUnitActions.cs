namespace MK.Logic.Runtime
{
    /// <summary>Spend card-granted influence on an owned unit, independently of its wound state.</summary>
    public static class CardUnitActions
    {
        public static bool TryReady(PlayerState player, ActionContext context, UnitState unit)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.IsReady
                || context.ReadyInfluencePerLevel <= 0 || unit.Card.Level < 1
                || unit.Card.Level > context.ReadyUnitMaxLevel) return false;
            int cost = unit.Card.Level * context.ReadyInfluencePerLevel;
            if (context.InfluencePool < cost) return false;
            context.InfluencePool -= cost;
            unit.Ready();
            return true;
        }
    }
}
