namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 治療或抽牌二選一的效果。
    /// </summary>
    public sealed class HealOrDrawEffect : ICardEffect, ICardEffectValidator
    {
        private readonly int _heal;
        private readonly int _draw;
        public HealOrDrawEffect(int heal, int draw)
        {
            _heal = heal;
            _draw = draw;
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 1) throw new System.InvalidOperationException("宁静必须选择治疗或抽牌");
            // The printed hand icon restricts the whole card, including its draw alternative.
            if (ctx.InBattle) throw new System.InvalidOperationException("战斗中不能使用宁静的治疗或抽牌效果");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0)
            {
                ctx.Healing.Add(_heal);
            }
            else
            {
                player.Deck.DrawExact(_draw);
            }
        }
    }
}
