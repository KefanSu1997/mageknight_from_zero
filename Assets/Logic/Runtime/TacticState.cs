using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>One selected original tactic; reset only at a real round boundary.</summary>
    public sealed class TacticState
    {
        public TacticCard Card { get; }
        public bool SelectionResolved { get; internal set; }
        public bool Spent { get; internal set; }
        public bool ManaSearchUsedThisTurn { get; internal set; }
        public ManaDie ReservedDie { get; internal set; }
        internal TacticState(TacticCard card)
        {
            Card = card;
            SelectionResolved = card.Id != "day_2" && card.Id != "day_3" && card.Id != "night_5";
        }
    }
}
