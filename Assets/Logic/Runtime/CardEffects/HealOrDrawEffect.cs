namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 治療或抽牌二選一的效果。
    /// </summary>
    public sealed class HealOrDrawEffect : ICardEffect
    {
        private readonly int _heal;
        private readonly int _draw;
        public HealOrDrawEffect(int heal, int draw)
        {
            _heal = heal;
            _draw = draw;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
            {
                CardHealing.Heal(player, ctx, _heal);
            }
            else
            {
                player.Deck.DrawExact(_draw);
            }
        }
    }
}
