namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 累計遠程攻擊力的效果。
    /// </summary>
    public sealed class RangedAttackEffect : ICardEffect
    {
        private readonly int _value;
        public RangedAttackEffect(int value) => _value = value;
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += _value;
        }
    }
}
