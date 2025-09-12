namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 反戈一擊：基礎攻擊2並每格擋+2，強效攻擊4並每格擋+3。
    /// </summary>
    public sealed class CounterStrikeEffect : ICardEffect
    {
        private readonly bool _enh;
        public CounterStrikeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh)
            {
                ctx.MeleePool += 4;
                ctx.AttackBonusPerBlock = 3;
            }
            else
            {
                ctx.MeleePool += 2;
                ctx.AttackBonusPerBlock = 2;
            }
        }
    }
}
