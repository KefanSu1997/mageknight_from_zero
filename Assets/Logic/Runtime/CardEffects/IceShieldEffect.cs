namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 寒冰之盾：強化時降低一名被格擋敵人的護甲。
    /// option 指定目標索引。
    /// </summary>
    public sealed class IceShieldEffect : ICardEffect
    {
        private readonly bool _enh;
        public IceShieldEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.BlockPool += 3; // 皆提供寒冰格擋3
            if (_enh)
            {
                ctx.ArmorReduction[option] = 3;
            }
        }
    }
}
