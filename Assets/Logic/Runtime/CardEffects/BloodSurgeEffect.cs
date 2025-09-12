namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 血脈賁張：創傷進手牌時抽牌，強效首次忽略創傷。
    /// </summary>
    public sealed class BloodSurgeEffect : ICardEffect
    {
        private readonly bool _enh;
        public BloodSurgeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.WoundDrawRemaining = 3;
            if (_enh)
                ctx.FirstWoundIgnored = true;
        }
    }
}
