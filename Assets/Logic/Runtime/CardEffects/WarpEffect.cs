namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 空間扭曲 / 時間扭曲：提供傳送與額外回合效果。
    /// </summary>
    public sealed class WarpEffect : ICardEffect
    {
        private readonly bool _enh;
        public WarpEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.TeleportRange = 2;
                ctx.IgnoreRampaging = true;
            }
            else
            {
                ctx.SkipDraw = true;
                ctx.ExtraTurn = true;
            }
        }
    }
}
