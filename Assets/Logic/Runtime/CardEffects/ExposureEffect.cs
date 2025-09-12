namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 暴露 / 群體暴露：無視護城與抗性並提供遠程攻擊。
    /// option 為 0 目標單體，-1 全體。
    /// </summary>
    public sealed class ExposureEffect : ICardEffect
    {
        private readonly bool _enh;
        public ExposureEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.IgnoreFortified = true;
            ctx.IgnoreResist = true;
            ctx.RangedPool += _enh ? 3 : 2;
        }
    }
}
