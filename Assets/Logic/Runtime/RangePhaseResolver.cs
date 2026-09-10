using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    internal static class RangedPhaseResolver
    {
        public static RangedResult Resolve(IList<Monster> enemies,
            IReadOnlyList<AttackAllocation> attacks, ActionContext ctx = null)
        {
            var killed = new HashSet<int>();
            int fame = 0;
            foreach (var attack in attacks)
            {
                var targets = (attack.TargetIndices == null || attack.TargetIndices.Count == 0
                    ? Enumerable.Range(0, enemies.Count) : attack.TargetIndices)
                    .Where(i => i >= 0 && i < enemies.Count && !killed.Contains(i)).Distinct().ToArray();
                if (targets.Length == 0) continue;
                var parts = CombatMath.Parts(attack);
                if (!CombatMath.CanAttack(enemies, targets, parts, Phase.Ranged, ctx)) continue;
                int armor = targets.Sum(i => ctx != null && ctx.ArmorReduction.TryGetValue(i, out int reduction)
                    ? System.Math.Max(1, enemies[i].Armor - reduction) : enemies[i].Armor);
                int effective = CombatMath.EffectiveAttack(enemies, targets, parts, Phase.Ranged, ctx);
                if (effective < armor) continue;
                foreach (int i in targets)
                    if (killed.Add(i)) fame += enemies[i].Fame;
            }
            return new RangedResult(killed.OrderBy(i => i).ToArray(), fame);
        }
    }
}
