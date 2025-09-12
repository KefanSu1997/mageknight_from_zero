using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 技能牌供應，簡化為固定列表與抽取機制。
    /// </summary>
    public sealed class SkillSupply
    {
        private readonly Deck<SkillCard> _deck = new();
        public readonly List<SkillCard> Offer = new();

        public void SetDeck(IEnumerable<SkillCard> skills) => _deck.Set(skills);

        public void Refill(int count = 3)
        {
            while (Offer.Count < count && _deck.TryDraw(out var c))
                Offer.Add(c);
        }

        public SkillCard Take(int index)
        {
            var c = Offer[index];
            Offer.RemoveAt(index);
            return c;
        }
    }
}
