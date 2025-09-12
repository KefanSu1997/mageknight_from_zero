namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 胁迫：在影響力與攻擊間二選一，同時降低聲望。
    /// option=0 為影響力，option=1 為攻擊。
    /// </summary>
    public sealed class CoercionEffect : ICardEffect
    {
        private readonly bool _enh;
        public CoercionEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (option == 0)
                    ctx.InfluencePool += 4;
                else
                    ctx.MeleePool += 3;
                player.Reputation -= 1;
            }
            else
            {
                if (option == 0)
                    ctx.InfluencePool += 8;
                else
                    ctx.MeleePool += 7;
                player.Reputation -= 2;
            }
        }
    }
}
