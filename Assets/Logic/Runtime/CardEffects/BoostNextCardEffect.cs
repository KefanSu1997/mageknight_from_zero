namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 使下一張行動牌免費強效並獲得數值加成的效果。
    /// </summary>
    public sealed class BoostNextCardEffect : ICardEffect
    {
        private readonly int _bonus;
        public BoostNextCardEffect(int bonus) => _bonus = bonus;
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.NextCardEnhanced = true;
            ctx.BoostValue = _bonus;
        }
    }
}
