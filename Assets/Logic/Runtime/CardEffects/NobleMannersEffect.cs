namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 高貴氣質：在交涉時可額外獲得名望與聲望。
    /// </summary>
    public sealed class NobleMannersEffect : ICardEffect
    {
        private readonly int _inf;
        private readonly bool _enh;
        public NobleMannersEffect(int influence, bool enhanced)
        {
            _inf = influence;
            _enh = enhanced;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += _inf;
            if (ctx.InNegotiation)
            {
                player.Fame += 1;
                if (_enh)
                    player.Reputation += 1;
            }
        }
    }
}
