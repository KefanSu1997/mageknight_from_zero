namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 時光傳送：可瞬移並下次抽牌上限提高。
    /// </summary>
    public sealed class TimeBendingEffect : ICardEffect
    {
        private readonly bool _enh;
        public TimeBendingEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.TeleportRange = _enh ? 2 : 1;
            ctx.NextDrawBonus = _enh ? 2 : 1;
        }
    }
}
