namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 提供固定移動力的效果。
    /// </summary>
    public sealed class MoveEffect : ICardEffect
    {
        private readonly int _value;
        public MoveEffect(int value) => _value = value;
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.TransformMoveType == 1)
            {
                ctx.MeleePool += _value;
            }
            else if (ctx.TransformMoveType == 2)
            {
                ctx.BlockPool += _value;
            }
            else
            {
                ctx.MovementPool += _value;
            }
            ctx.TransformMoveType = 0;
            if (ctx.MoveBonusNext != 0)
            {
                ctx.MovementPool += ctx.MoveBonusNext;
                ctx.MoveBonusNext = 0;
            }
            if (ctx.MoveBonusAll != 0)
            {
                ctx.MovementPool += ctx.MoveBonusAll;
            }
        }
    }
}
