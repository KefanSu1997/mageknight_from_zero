namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 利斧投擲：基礎可移動或遠攻，強效遠攻且若擊殺敵人獲得名望。
    /// </summary>
    public sealed class ThrowingAxeEffect : ICardEffect
    {
        private readonly bool _enh;
        public ThrowingAxeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (option == 0)
                    ctx.MovementPool += 2;
                else
                    ctx.RangedPool += 1;
            }
            else
            {
                ctx.RangedPool += 3;
                ctx.PendingFameOnRangedKill = 1;
            }
        }
    }
}
