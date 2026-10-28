using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>One selected use of an original repeated effect; references real card instances.</summary>
    public sealed class RepeatedCardSelection
    {
        public int Option { get; set; }
        public DeedCard CardToRemove { get; set; }
        public DeedCard CardToDiscard { get; set; }

        public bool Uses(DeedCard card) => ReferenceEquals(CardToRemove, card) || ReferenceEquals(CardToDiscard, card);
    }
}
