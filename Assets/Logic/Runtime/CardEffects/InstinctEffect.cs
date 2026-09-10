using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>原版本能：移动、影响、物理攻击或格挡四选一，基础2/强效4。</summary>
    public sealed class InstinctEffect : ICardEffect, ICardEffectValidator
    {
        private readonly int _value;
        public InstinctEffect(int value) => _value = value;
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > 3) throw new InvalidOperationException("本能必须选择移动、影响、攻击或格挡");
        }
        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            switch (option)
            {
                case 0: context.MovementPool += _value; break;
                case 1: context.InfluencePool += _value; break;
                case 2: context.CombatPower.AddAttack(_value); break;
                case 3: context.CombatPower.AddBlock(_value); break;
            }
        }
    }
}
