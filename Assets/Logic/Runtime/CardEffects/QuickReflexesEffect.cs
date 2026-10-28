
using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 迅捷反應：三擇一，移動、遠程攻擊或降低一次敵人攻擊。
    /// </summary>
    public sealed class QuickReflexesEffect : ICardEffect, IFixedCardOutput, ICardEffectValidator
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

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 2)
                throw new InvalidOperationException("迅捷反应必须选择移动、远程攻击或减攻之一");
        }

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] {
            new FixedCardOutput(0, FixedOutputKind.Movement, _move),
            new FixedCardOutput(1, FixedOutputKind.Attack, _ranged, AttackType: MK.Logic.Core.AttackType.Ranged) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            switch (option)
            {
                case 0:
                    FixedCardOutputs.Add(ctx, FixedOutputs[0]);
                    break;
                case 1:
                    FixedCardOutputs.Add(ctx, FixedOutputs[1]);
                    break;
                default:
                    ctx.AttackReduction += _reduce;
                    ctx.AttackReductionTimes = 1;
                    break;
            }
        }
    }
}
