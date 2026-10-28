using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 累計遠程攻擊力的效果。
    /// </summary>
    public sealed class RangedAttackEffect : ICardEffect, IFixedCardOutput
    {
        private readonly int _value;
        private readonly Element _element;
        private readonly AttackType _type;
        public RangedAttackEffect(int value, Element element = Element.Physical, AttackType type = AttackType.Ranged)
        { _value = value; _element = element; _type = type; }
        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] { new FixedCardOutput(0, FixedOutputKind.Attack, _value, _element, _type) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            FixedCardOutputs.Add(ctx, FixedOutputs[0]);
        }
    }
}
