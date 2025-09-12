namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 騎士精神：攻擊並依擊殺獲得聲望/名望。
    /// option 0 普通攻擊，1 帶加值效果。
    /// </summary>
    public sealed class KnightlySpiritEffect : ICardEffect
    {
        private readonly bool _enh;
        public KnightlySpiritEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
            {
                ctx.MeleePool += _enh ? 6 : 3;
            }
            else
            {
                ctx.MeleePool += _enh ? 4 : 2;
                ctx.PendingReputationOnKill = 1;
                if (_enh) ctx.PendingFameOnKill = 1;
            }
        }
    }
}
