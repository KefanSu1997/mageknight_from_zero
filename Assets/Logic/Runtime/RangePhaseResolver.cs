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
            ctx ??= new ActionContext();
            var killed = new HashSet<int>();
            int fame = 0;
            int reputation = 0;
            var attackCrystals = new Dictionary<int, int>();
            int armorUses = 0;
            foreach (var attack in attacks)
            {
                var targets = (attack.TargetIndices == null || attack.TargetIndices.Count == 0
                    ? Enumerable.Range(0, enemies.Count) : attack.TargetIndices)
                    .Where(i => i >= 0 && i < enemies.Count && !killed.Contains(i) && ctx?.DefeatedEnemyIndices.Contains(i) != true).Distinct().ToArray();
                if (targets.Length == 0) continue;
                var parts = CombatMath.Parts(attack);
                if (!CombatMath.CanAttack(enemies, targets, parts, Phase.Ranged, ctx)) continue;
                ctx.EnemyDefense.Apply(enemies, targets, ctx, killed);
                if (ctx != null)
                    foreach (int target in targets) DuelActions.RecordUnitAttack(ctx, enemies[target], parts, Phase.Ranged);
                int armor = targets.Sum(i => CombatMath.EffectiveArmor(enemies[i], i, ctx, Phase.Ranged));
                int effective = CombatMath.EffectiveAttack(enemies, targets, parts, Phase.Ranged, ctx);
                if (effective < armor) continue;
                var reward = CombatMath.AttackRewards(parts, targets.Length);
                fame += reward.Fame;
                reputation += reward.Reputation;
                foreach (int i in targets)
                    if (killed.Add(i))
                    {
                        fame += enemies[i].Fame + EnemyRewardRules.TakeBonus(ctx, enemies[i], EnemyDefeatSource.Ranged);
                        int crystals = parts.Sum(p => p.CrystalsPerDefeatedEnemy);
                        if (crystals > 0) attackCrystals[i] = crystals;
                        armorUses += parts.Sum(p => p.ArmorReductionsPerDefeatedEnemy);
                    }
            }
            return new RangedResult(killed.OrderBy(i => i).ToArray(), fame, reputation, attackCrystals, armorUses);
        }
    }
}
