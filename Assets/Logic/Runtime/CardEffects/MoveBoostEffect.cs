namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 永不止步：移動並提升後續移動牌的效果。
    /// </summary>
    public sealed class MoveBoostEffect : ICardEffect, IFixedCardOutput
    {
        private readonly int _move;
        private readonly bool _all;
        public MoveBoostEffect(int move, bool all)
        {
            _move = move;
            _all = all;
        }

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] { new FixedCardOutput(0, FixedOutputKind.Movement, _move) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            FixedCardOutputs.Add(ctx, FixedOutputs[0]);
            if (_all)
                ctx.MoveBonusAll += 1;
            else
                ctx.MoveBonusNext = 1;
        }
    }
}
