namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 靈動：提供移動力並允許以移動力兌換攻擊/遠程攻擊。
    /// </summary>
    public sealed class AgilityEffect : ICardEffect
    {
        private readonly bool _enh;
        public AgilityEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 2;
                ctx.MoveCostAttack = 1;
                ctx.MoveCostRanged = 0;
            }
            else
            {
                ctx.MovementPool += 4;
                ctx.MoveCostAttack = 1;
                ctx.MoveCostRanged = 2;
            }
        }
    }
}
