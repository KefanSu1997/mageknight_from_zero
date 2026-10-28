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
            ctx.CombatPower.AddBlock(_enh ? 5 : 3, doubleAgainstSwift: true, reduceArmorByExcess: _enh);
        }
    }
}
