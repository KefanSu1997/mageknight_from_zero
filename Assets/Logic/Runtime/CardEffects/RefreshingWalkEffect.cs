namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 提神漫步：非戰鬥時可治療，戰鬥中僅提供移動力。
    /// </summary>
    public sealed class RefreshingWalkEffect : ICardEffect
    {
        private readonly bool _enh;
        public RefreshingWalkEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 2;
                if (!ctx.InBattle)
                    CardHealing.Heal(player, ctx, 1);
            }
            else
            {
                ctx.MovementPool += 4;
                if (!ctx.InBattle)
                    CardHealing.Heal(player, ctx, 2);
            }
        }
    }
}
