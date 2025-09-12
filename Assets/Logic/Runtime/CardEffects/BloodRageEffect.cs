namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 血色狂怒：根據是否自願受創提升攻擊力。
    /// option=0 不受創，option=1 受創提升。
    /// </summary>
    public sealed class BloodRageEffect : ICardEffect
    {
        private readonly bool _enh;
        public BloodRageEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            bool wound = option != 0;
            if (!_enh)
            {
                ctx.MeleePool += wound ? 5 : 2;
            }
            else
            {
                ctx.MeleePool += wound ? 9 : 4;
            }
            if (wound)
                player.Wounds += 1;
        }
    }
}
