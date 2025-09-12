namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 增加影響力數值的效果。
    /// </summary>
    public sealed class InfluenceEffect : ICardEffect
    {
        private readonly int _value;
        public InfluenceEffect(int value) => _value = value;
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += _value;
        }
    }
}
