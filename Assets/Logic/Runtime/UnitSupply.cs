using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Actual unit cards: recruitment leaves a vacancy until the next round.
    /// The scenario supplies physical copies and their initial order.</summary>
    public sealed class UnitSupply
    {
        private readonly Queue<UnitCard> _regular = new();
        private readonly Queue<UnitCard> _elite = new();
        public List<UnitCard> Offer { get; } = new();
        public int RemainingCount => _regular.Count + _elite.Count;
        public void SetDeck(IEnumerable<UnitCard> cards)
        {
            var actual=cards?.ToArray()??Array.Empty<UnitCard>();
            if(actual.Any(c=>c==null) || actual.Where((c,i)=>actual.Take(i).Any(x=>ReferenceEquals(x,c))).Any())
                throw new InvalidOperationException("部队供应必须由不同的实际原牌实例组成");
            Offer.Clear();_regular.Clear();_elite.Clear();
            foreach(var card in actual)(card.IsElite?_elite:_regular).Enqueue(card);
        }
        public int AppendRegularToOffer(int maximum = 2)
        {
            if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            int count = Math.Min(maximum, _regular.Count);
            for (int i=0;i<count;i++) Offer.Add(_regular.Dequeue());
            return count;
        }
        public bool TryAppendEliteToOffer()
        {
            if (_elite.Count == 0) return false;
            Offer.Add(_elite.Dequeue());
            return true;
        }
        public void StartRound(int players,bool coreRevealed)
        {
            if(players<1)throw new ArgumentOutOfRangeException(nameof(players));
            foreach(var card in Offer)(card.IsElite?_elite:_regular).Enqueue(card);
            Offer.Clear();
            for(int i=0;i<players+2;i++)
            {
                var deck=coreRevealed && i%2==0?_elite:_regular;
                if(deck.Count>0)Offer.Add(deck.Dequeue());
            }
        }
    }
}
