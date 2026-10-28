using System;
using System.Collections.Generic;
using System.Linq;
namespace MK.Logic.Runtime.CardEffects
{
    public sealed class DeterminationEffect : ICardEffect, IFixedCardOutput
    {
        private readonly bool _enh;
        public DeterminationEffect(bool enhanced) => _enh = enhanced;
        public IReadOnlyList<FixedCardOutput> FixedOutputs => _enh ? new[] { new FixedCardOutput(0, FixedOutputKind.Block, 5) } : new[] { new FixedCardOutput(0, FixedOutputKind.Attack, 2), new FixedCardOutput(1, FixedOutputKind.Block, 2) };
        public void Execute(PlayerState player, ActionContext context, int option = 0)
            => FixedCardOutputs.Add(context, FixedOutputs.Single(o => o.Option == option));
    }
}
