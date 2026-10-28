using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Rulebook p24: armor gained belongs to the actual attacker until this battle ends.</summary>
    public sealed class VampirismState
    {
        private readonly List<(Monster Enemy, int Amount)> gains = new();
        public int Armor(Monster enemy) => gains.Where(g => ReferenceEquals(g.Enemy, enemy)).Sum(g => g.Amount);
        internal void Add(Monster enemy, int amount)
        { if (amount > 0) gains.Add((enemy, amount)); }
        internal void Clear() => gains.Clear();
    }
}
