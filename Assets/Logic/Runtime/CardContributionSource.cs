using System;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Bind power to the physical card only while its formal effect is executing.</summary>
    internal sealed class CardContributionSource : IDisposable
    {
        private readonly ActionContext _context;
        private readonly object _previousContext;
        private readonly object _previousPool;

        private CardContributionSource(ActionContext context, object source)
        {
            _context = context;
            _previousContext = context.CurrentSourceCard;
            _previousPool = context.CombatPower.CurrentSourceCard;
            context.CurrentSourceCard = source;
            context.CombatPower.CurrentSourceCard = source;
        }

        internal static IDisposable Scope(ActionContext context, object source)
            => new CardContributionSource(context, source);

        public void Dispose()
        {
            _context.CurrentSourceCard = _previousContext;
            _context.CombatPower.CurrentSourceCard = _previousPool;
        }
    }
}
