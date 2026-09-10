using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 累計遠程攻擊力的效果。
    /// </summary>
    public sealed class RangedAttackEffect : ICardEffect
    {
        private readonly int _value;
        private readonly Element _element;
        private readonly AttackType _type;
        public RangedAttackEffect(int value, Element element = Element.Physical, AttackType type = AttackType.Ranged)
        { _value = value; _element = element; _type = type; }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.CombatPower.AddAttack(_value, _element, _type);
        }
    }
}
