namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 盾牌重擊：格擋值計雙倍對 Swift，強效額外削弱護甲。
    /// </summary>
    public sealed class ShieldBashEffect : ICardEffect
    {
        private readonly bool _enh;
        public ShieldBashEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (_enh)
            {
                ctx.BlockPool += 5;
                ctx.SwiftBlockBonus += 5;
            }
            else
            {
                ctx.BlockPool += 3;
                ctx.SwiftBlockBonus += 3;
            }
        }
    }
}
