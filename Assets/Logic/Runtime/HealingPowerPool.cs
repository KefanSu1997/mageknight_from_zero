using System;

namespace MK.Logic.Runtime
{
    /// <summary>Unspent healing, retaining the Golden Grail's point-specific Fame reward.</summary>
    public sealed class HealingPowerPool
    {
        public int Ordinary { get; private set; }
        public int Grail { get; private set; }
        public int Total => Ordinary + Grail;

        public void Add(int points, bool earnsGrailFame = false)
        {
            if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
            if (earnsGrailFame) Grail += points;
            else Ordinary += points;
        }

        /// <returns>Fame earned only from Grail points actually spent.</returns>
        public int Spend(int points, bool grailFirst = true)
        {
            if (points <= 0 || points > Total) throw new InvalidOperationException("治疗点不足或消费数量无效");
            int rewarded = grailFirst ? Math.Min(points, Grail) : Math.Max(0, points - Ordinary);
            Grail -= rewarded;
            Ordinary -= points - rewarded;
            return rewarded;
        }

        public void Clear() => Ordinary = Grail = 0;
    }
}
