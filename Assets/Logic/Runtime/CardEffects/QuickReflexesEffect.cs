
namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 迅捷反應：三擇一，移動、遠程攻擊或降低一次敵人攻擊。
    /// </summary>
    public sealed class QuickReflexesEffect : ICardEffect
    {
        private readonly int _move;
        private readonly int _ranged;
        private readonly int _reduce;
        public QuickReflexesEffect(int move, int ranged, int reduce)
        {
            _move = move;
            _ranged = ranged;
            _reduce = reduce;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            switch (option)
            {
                case 0:
                    ctx.MovementPool += _move;
                    break;
                case 1:
                    ctx.RangedPool += _ranged;
                    break;
                default:
                    ctx.AttackReduction += _reduce;
                    break;
            }
        }
    }
}
