namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 永不止步：移動並提升後續移動牌的效果。
    /// </summary>
    public sealed class MoveBoostEffect : ICardEffect
    {
        private readonly int _move;
        private readonly bool _all;
        public MoveBoostEffect(int move, bool all)
        {
            _move = move;
            _all = all;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _move;
            if (_all)
                ctx.MoveBonusAll += 1;
            else
                ctx.MoveBonusNext = 1;
        }
    }
}
