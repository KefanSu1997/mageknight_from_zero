using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 战斗阶段中的伤害封装，用于状态结算。
    /// </summary>
    public sealed class DamagePacket
    {
        public Element Element { get; init; }
        public int Amount { get; set; }
        public AttackType AttackType { get; init; }

        public DamagePacket(Element element, int amount, AttackType attackType = AttackType.Melee)
        {
            Element = element;
            Amount = amount;
            AttackType = attackType;
        }
    }
}