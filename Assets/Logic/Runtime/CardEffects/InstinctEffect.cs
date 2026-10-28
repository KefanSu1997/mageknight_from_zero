using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>原版本能：移动、影响、物理攻击或格挡四选一，基础2/强效4。</summary>
    public sealed class InstinctEffect : ICardEffect, ICardEffectValidator, IFixedCardOutput
    {
        private readonly int _value;
        public InstinctEffect(int value) => _value = value;
        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] {
            new FixedCardOutput(0, FixedOutputKind.Movement, _value),
            new FixedCardOutput(2, FixedOutputKind.Attack, _value),
            new FixedCardOutput(3, FixedOutputKind.Block, _value) };
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > 3) throw new InvalidOperationException("本能必须选择移动、影响、攻击或格挡");
        }
        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            if (option == 1) context.InfluencePool += _value;
            else FixedCardOutputs.Add(context, System.Linq.Enumerable.Single(FixedOutputs, o => o.Option == option));
        }
    }
}
