namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 決心：基礎提供攻擊或格擋，強效直接格擋5點。
    /// </summary>
    public sealed class DeterminationEffect : ICardEffect
    {
        private readonly bool _enh;
        public DeterminationEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                if (option == 0)
                    ctx.MeleePool += 2;
                else
                    ctx.BlockPool += 2;
            }
            else
            {
                ctx.BlockPool += 5;
            }
        }
    }
}
