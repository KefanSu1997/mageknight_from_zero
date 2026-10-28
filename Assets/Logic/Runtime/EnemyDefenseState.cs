using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed record EnemyDefendChoice(int SourceIndex, int TargetIndex, int Value);
    public sealed record EnemyDefendAssignment(Monster Source, Monster Target, int Value);

    /// <summary>Tezla p2: each living, unused defender protects one distinct attacked token.
    /// Choices are provisional until an actual attack; applied bonuses last through combat.</summary>
    public sealed class EnemyDefenseState
    {
        private readonly List<EnemyDefendAssignment> applied = new();
        private readonly List<EnemyDefendAssignment> selected = new();
        private Monster[] selectionTargets = Array.Empty<Monster>();
        public IReadOnlyList<EnemyDefendAssignment> Applied => applied.AsReadOnly();
        public IReadOnlyList<EnemyDefendAssignment> Selected => selected.AsReadOnly();
        public int Bonus(Monster target) => applied.Where(a => ReferenceEquals(a.Target, target)).Sum(a => a.Value);
        public void ClearChoices() { selected.Clear(); selectionTargets = Array.Empty<Monster>(); }
        public void Clear() { applied.Clear(); ClearChoices(); }

        private List<EnemyDefendAssignment> Plan(IList<Monster> enemies, IReadOnlyList<int> targets,
            ActionContext ctx, ISet<int> dead, out List<EnemyDefendChoice> choices)
        {
            if (targets == null || targets.Count == 0 || targets.Distinct().Count() != targets.Count
                || targets.Any(i => i < 0 || i >= enemies.Count || dead.Contains(i)))
                throw new InvalidOperationException("攻击目标无效");
            bool same = selectionTargets.Length == targets.Count
                && selectionTargets.All(e => targets.Any(i => ReferenceEquals(e, enemies[i])));
            var plan = new List<EnemyDefendAssignment>();
            var sources = Enumerable.Range(0, enemies.Count).Where(i => !dead.Contains(i)
                && enemies[i].DefendValue > 0 && enemies[i].Abilities.Contains(Ability.Defend)
                && ctx?.InsightIgnores(enemies[i], Ability.Defend) != true
                && !applied.Any(a => ReferenceEquals(a.Source, enemies[i]))).ToList();
            var eligible = targets.Where(i => !applied.Any(a => ReferenceEquals(a.Target, enemies[i]))).ToList();
            if (same)
                foreach (var selection in selected)
                {
                    int source = sources.FindIndex(i => ReferenceEquals(enemies[i], selection.Source));
                    int target = eligible.FindIndex(i => ReferenceEquals(enemies[i], selection.Target));
                    if (source < 0 || target < 0) throw new InvalidOperationException("防御预选已失效，请重新选择");
                    plan.Add(selection); sources.RemoveAt(source); eligible.RemoveAt(target);
                }
            if (sources.Count == 1 && eligible.Count == 1)
            {
                plan.Add(new EnemyDefendAssignment(enemies[sources[0]], enemies[eligible[0]], enemies[sources[0]].DefendValue));
                sources.Clear(); eligible.Clear();
            }
            choices = sources.SelectMany(s => eligible.Select(t => new EnemyDefendChoice(s, t, enemies[s].DefendValue))).ToList();
            return plan;
        }
        private static HashSet<int> Dead(ActionContext ctx, IEnumerable<int> additional = null)
            => new HashSet<int>((ctx?.DefeatedEnemyIndices ?? new HashSet<int>()).Concat(additional ?? Array.Empty<int>()));
        public IReadOnlyList<EnemyDefendChoice> Choices(IList<Monster> enemies, IReadOnlyList<int> targets, ActionContext ctx)
        { Plan(enemies, targets, ctx, Dead(ctx), out var choices); return choices; }
        public void Choose(IList<Monster> enemies, IReadOnlyList<int> targets, ActionContext ctx, int source, int target)
        {
            Plan(enemies, targets, ctx, Dead(ctx), out var choices);
            var choice = choices.SingleOrDefault(c => c.SourceIndex == source && c.TargetIndex == target);
            if (choice == null) throw new InvalidOperationException("请选择实际未应用的防御来源及本次攻击目标");
            if (selectionTargets.Length != targets.Count || !selectionTargets.All(e => targets.Any(i => ReferenceEquals(e, enemies[i]))))
                ClearChoices();
            selectionTargets = targets.Select(i => enemies[i]).ToArray();
            selected.Add(new EnemyDefendAssignment(enemies[source], enemies[target], choice.Value));
        }
        public IReadOnlyList<EnemyDefendAssignment> PreviewAssignments(IList<Monster> enemies, IReadOnlyList<int> targets, ActionContext ctx)
        {
            var plan = Plan(enemies, targets, ctx, Dead(ctx), out var choices);
            if (choices.Count > 0) throw new InvalidOperationException("请先选择敌人防御的来源与目标");
            return plan;
        }
        public void Apply(IList<Monster> enemies, IReadOnlyList<int> targets, ActionContext ctx, IEnumerable<int> additionalDead = null)
        {
            var plan = Plan(enemies, targets, ctx, Dead(ctx, additionalDead), out var choices);
            if (choices.Count > 0) throw new InvalidOperationException("请先选择敌人防御的来源与目标");
            applied.AddRange(plan); ClearChoices();
        }
    }
}
