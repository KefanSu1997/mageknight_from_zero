using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    public enum SoloDummyDualColorRule { Unspecified, MaximumMatchingCrystals, SumMatchingCrystals }
    public sealed record SoloDummyCard(string PhysicalId, string DefinitionId, ManaColor Color, ManaColor? SecondColor=null);
    // Private checkpoint data. Never include Draw/Cards/RandomState in the policy observation.
    public sealed record SoloDummySnapshot(int Version, int Round, SoloDummyCard[] Cards,
        SoloDummyCard[] Draw, SoloDummyCard[] Discard, int[] Crystals, ulong RandomState,
        bool Announced, bool Closed, SoloDummyDualColorRule DualColorRule=SoloDummyDualColorRule.Unspecified);
    public sealed record SoloDummyPublicState(int Round, int Remaining, int[] Crystals,
        SoloDummyCard[] Discard, bool Announced);

    /// <summary>Physical dummy deck, original rulebook p15; independent checkpointed RNG.</summary>
    public sealed class SoloDummyDeck
    {
        private List<SoloDummyCard> _cards, _draw, _discard;
        private int[] _crystals;
        private ulong _rng;
        public int Round { get; private set; } = 1;
        public bool Announced { get; private set; }
        public bool Closed { get; private set; }
        public int Remaining => _draw.Count;
        public SoloDummyDualColorRule DualColorRule { get; private set; }
        private SoloDummyDeck() { }

        public SoloDummyDeck(IEnumerable<SoloDummyCard> originalSixteen, int[] printedCrystals, ulong seed,
            SoloDummyDualColorRule dualColorRule=SoloDummyDualColorRule.Unspecified)
        {
            if(!Enum.IsDefined(typeof(SoloDummyDualColorRule),dualColorRule))throw new ArgumentException("Unknown dual-color rule.");
            DualColorRule=dualColorRule;
            _cards = originalSixteen.ToList();
            ValidateCards(_cards);
            ValidateDualRule(_cards);
            if (_cards.Count != 16 || printedCrystals == null || printedCrystals.Length != 4
                || printedCrystals.Any(n => n < 0) || printedCrystals.Sum() != 3 || seed == 0)
                throw new ArgumentException("Need original sixteen cards, three printed crystals and nonzero RNG seed.");
            _crystals = (int[])printedCrystals.Clone();
            _rng = seed;
            _draw = _cards.ToList(); _discard = new List<SoloDummyCard>();
            Shuffle();
        }

        private static void ValidateCards(IEnumerable<SoloDummyCard> cards)
        {
            var list = cards.ToArray();
            if (list.Any(c => c == null || string.IsNullOrEmpty(c.PhysicalId) || string.IsNullOrEmpty(c.DefinitionId)
                || (int)c.Color < 0 || (int)c.Color >= 4
                || c.SecondColor.HasValue && ((int)c.SecondColor.Value<0 || (int)c.SecondColor.Value>=4 || c.SecondColor==c.Color))
                || list.Select(c=>c.PhysicalId).Distinct().Count()!=list.Length)
                throw new ArgumentException("Dummy needs unique physical cards with printed basic colors.");
        }
        private void ValidateDualRule(IEnumerable<SoloDummyCard> cards)
        {
            if(DualColorRule==SoloDummyDualColorRule.Unspecified && cards.Any(c=>c.SecondColor.HasValue))
                throw new InvalidOperationException("Dual-color dummy cards require an explicitly selected rule.");
        }

        private ulong NextRandom()
        {
            // xorshift64*: algorithm and state explicitly versioned in Snapshot v1.
            _rng ^= _rng >> 12; _rng ^= _rng << 25; _rng ^= _rng >> 27;
            return unchecked(_rng * 2685821657736338717UL);
        }
        private int NextIndex(int count)
        {
            ulong n=(ulong)count, threshold=unchecked(0UL-n)%n, value;
            do { value=NextRandom(); } while(value<threshold);
            return (int)(value%n);
        }
        private void Shuffle()
        {
            for(int i=_draw.Count-1;i>0;i--){int j=NextIndex(i+1);(_draw[i],_draw[j])=(_draw[j],_draw[i]);}
        }

        public bool TakeTurn()
        {
            if (Closed || Announced) throw new InvalidOperationException("Dummy round is closed or already announced.");
            if (_draw.Count == 0) { Announced=true; return true; }
            int initial=Math.Min(3,_draw.Count);
            var last=_draw[initial-1];
            int extra=_crystals[(int)last.Color];
            if(last.SecondColor.HasValue)
            {
                ValidateDualRule(new[]{last});
                int second=_crystals[(int)last.SecondColor.Value];
                extra=DualColorRule==SoloDummyDualColorRule.MaximumMatchingCrystals?Math.Max(extra,second):extra+second;
            }
            int total=Math.Min(_draw.Count,initial+extra);
            _discard.AddRange(_draw.Take(total)); _draw.RemoveRange(0,total);
            return false; // Last-card exhaustion is announced only on the NEXT turn.
        }

        public void CloseRound()
        {
            if(Closed)throw new InvalidOperationException("Round already closed.");
            Closed=true;
        }
        public void BeginNextRound(SoloDummyCard removedBottomAdvanced, ManaColor removedBottomSpellColor)
        {
            if(!Closed || Round>=6)throw new InvalidOperationException("No next dummy round is available.");
            ValidateCards(_cards.Concat(new[]{removedBottomAdvanced}));
            ValidateDualRule(new[]{removedBottomAdvanced});
            if((int)removedBottomSpellColor<0 || (int)removedBottomSpellColor>=4)throw new ArgumentException("Spell must have a basic color.");
            _cards.Add(removedBottomAdvanced); _crystals[(int)removedBottomSpellColor]++;
            _draw=_cards.ToList(); _discard.Clear(); Shuffle(); Round++; Closed=Announced=false;
        }
        public SoloDummyPublicState Observe() => new(Round,Remaining,(int[])_crystals.Clone(),_discard.ToArray(),Announced);
        public SoloDummySnapshot Capture() => new(2,Round,_cards.ToArray(),_draw.ToArray(),_discard.ToArray(),
            (int[])_crystals.Clone(),_rng,Announced,Closed,DualColorRule);
        internal void ApplyValidatedSnapshot(SoloDummySnapshot snapshot)
        {
            var copy=Restore(snapshot);
            Round=copy.Round; _cards=copy._cards; _draw=copy._draw; _discard=copy._discard;
            _crystals=copy._crystals; _rng=copy._rng; Announced=copy.Announced; Closed=copy.Closed;
            DualColorRule=copy.DualColorRule;
        }
        public static SoloDummyDeck Restore(SoloDummySnapshot s)
        {
            if(s==null || s.Version is not (1 or 2) || s.Round<1 || s.Round>6 || s.RandomState==0
                || !Enum.IsDefined(typeof(SoloDummyDualColorRule),s.DualColorRule)
                || s.Cards==null || s.Draw==null || s.Discard==null || s.Crystals==null
                || s.Crystals.Length!=4 || s.Crystals.Any(n=>n<0) || s.Crystals.Sum()!=s.Round+2
                || s.Cards.Length!=15+s.Round || s.Announced && s.Draw.Length!=0)
                throw new ArgumentException("Invalid dummy checkpoint.");
            ValidateCards(s.Cards); ValidateCards(s.Draw.Concat(s.Discard));
            if(s.Version==1 && (s.DualColorRule!=SoloDummyDualColorRule.Unspecified || s.Cards.Any(c=>c.SecondColor.HasValue))
                || s.DualColorRule==SoloDummyDualColorRule.Unspecified && s.Cards.Any(c=>c.SecondColor.HasValue))
                throw new ArgumentException("Checkpoint has unbound dual-color semantics.");
            if(!s.Cards.OrderBy(c=>c.PhysicalId).SequenceEqual(s.Draw.Concat(s.Discard).OrderBy(c=>c.PhysicalId)))
                throw new ArgumentException("Dummy physical inventory changed across zones.");
            return new SoloDummyDeck{Round=s.Round,_cards=s.Cards.ToList(),_draw=s.Draw.ToList(),_discard=s.Discard.ToList(),
                _crystals=(int[])s.Crystals.Clone(),_rng=s.RandomState,Announced=s.Announced,Closed=s.Closed,DualColorRule=s.DualColorRule};
        }
    }
}
