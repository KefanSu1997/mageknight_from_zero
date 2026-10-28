using System;
using System.Collections.Generic;
using System.Linq;
namespace MK.Logic.Runtime.CardEffects
{
    public sealed class MoveEffect : ICardEffect, IFixedCardOutput
    {
        private readonly int _value;
        public MoveEffect(int value) => _value = value;
        public IReadOnlyList<FixedCardOutput> FixedOutputs => new[] { new FixedCardOutput(0, FixedOutputKind.Movement, _value) };
        public void Execute(PlayerState player, ActionContext context, int option = 0)
            => FixedCardOutputs.Add(context, FixedOutputs.Single(o => o.Option == option));
    }
}
