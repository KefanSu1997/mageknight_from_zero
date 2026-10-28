using System.Collections.Generic;
using System.Linq;

namespace MK.Logic.Runtime
{
    public sealed partial class ActionContext
    {
        // Indexed effects belong to a physical enemy. A temporary defender must not
        // inherit its summoner's effects or leave its own effects on that summoner.
        internal ActionContext ExtractEnemyModifiers(ISet<int> indices)
        {
            var saved = new ActionContext();
            TransferEnemyModifiers(this, saved, indices);
            return saved;
        }

        internal void RestoreEnemyModifiers(ActionContext saved, ISet<int> indices)
        {
            ExtractEnemyModifiers(indices);
            TransferEnemyModifiers(saved, this, indices);
        }

        private static void TransferEnemyModifiers(ActionContext from, ActionContext to, ISet<int> indices)
        {
            Move(from.ArmorReduction, to.ArmorReduction, indices);
            Move(from.EnemyAttackAdjust, to.EnemyAttackAdjust, indices);
            Move(from.IgnoredAbilities, to.IgnoredAbilities, indices);
            Move(from.AttackElementChange, to.AttackElementChange, indices);
            Move(from._killCrystalChoices, to._killCrystalChoices, indices);
            MoveAttacks(from.PendingAttackReductions, to.PendingAttackReductions, indices);
            MoveAttacks(from.CancelledAttacks, to.CancelledAttacks, indices);
            MoveAttacks(from.SuccessfullyBlockedAttacks, to.SuccessfullyBlockedAttacks, indices);
            Move(from.SkipAttackIndices, to.SkipAttackIndices, indices);
            Move(from.PossessedEnemyIndices, to.PossessedEnemyIndices, indices);
            Move(from.FireResistRemoved, to.FireResistRemoved, indices);
            Move(from.FortificationsRemoved, to.FortificationsRemoved, indices);
            Move(from.ResistancesRemoved, to.ResistancesRemoved, indices);
            Move(from.NightWingsSkippedIndices, to.NightWingsSkippedIndices, indices);
            Move(from.DefeatedEnemyIndices, to.DefeatedEnemyIndices, indices);
            var targets = from.SpellTargetIndices.Where(indices.Contains).ToArray();
            from.SpellTargetIndices.RemoveAll(indices.Contains);
            to.SpellTargetIndices.AddRange(targets);
            if (from.KillEnemyIndex.HasValue && indices.Contains(from.KillEnemyIndex.Value))
            { to.KillEnemyIndex = from.KillEnemyIndex; from.KillEnemyIndex = null; }
        }

        private static void Move<T>(Dictionary<int, T> from, Dictionary<int, T> to, ISet<int> indices)
        {
            foreach (var pair in from.Where(p => indices.Contains(p.Key)).ToArray())
            { from.Remove(pair.Key); to.Add(pair.Key, pair.Value); }
        }

        private static void Move(HashSet<int> from, HashSet<int> to, ISet<int> indices)
        {
            foreach (int index in from.Where(indices.Contains).ToArray())
            { from.Remove(index); to.Add(index); }
        }

        private static void MoveAttacks<T>(Dictionary<(int Target, int Attack), T> from,
            Dictionary<(int Target, int Attack), T> to, ISet<int> indices)
        {
            foreach (var pair in from.Where(p => indices.Contains(p.Key.Target)).ToArray())
            { from.Remove(pair.Key); to.Add(pair.Key, pair.Value); }
        }

        private static void MoveAttacks(HashSet<(int Target, int Attack)> from,
            HashSet<(int Target, int Attack)> to, ISet<int> indices)
        {
            foreach (var pair in from.Where(p => indices.Contains(p.Target)).ToArray())
            { from.Remove(pair); to.Add(pair); }
        }
    }
}
