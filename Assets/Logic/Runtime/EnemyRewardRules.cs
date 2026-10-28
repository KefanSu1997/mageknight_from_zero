using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed record EnemyRewardGrant(Monster Enemy, int Fame, EnemyDefeatSource Source);

    /// <summary>Actual extra rewards of this combat. Retained after combat for result inspection.</summary>
    public sealed class EnemyRewardState
    {
        private readonly List<EnemyRewardGrant> grants = new();
        public IReadOnlyList<EnemyRewardGrant> Grants => grants.AsReadOnly();
        public int Count => grants.Count;
        public int TotalFame => grants.Sum(g => g.Fame);
        internal void BeginBattle() => grants.Clear();
        internal int Take(Monster enemy, EnemyDefeatSource source)
        {
            if (grants.Any(g => ReferenceEquals(g.Enemy, enemy))) return 0;
            grants.Add(new EnemyRewardGrant(enemy, 1, source));
            return 1;
        }
    }

    /// <summary>Official Tezla p3 "Just Fame": replace faction-token reward by one extra fame.
    /// Printed fame and combat abilities are deliberately independent of this configurable variant.</summary>
    public static class EnemyRewardRules
    {
        public static int GetFactionFame(Monster enemy, ActionContext context)
            => PreviewBonus(enemy, context?.EnemyRewardMode ?? EnemyRewardMode.PrintedFameOnly);
        public static int PreviewBonus(Monster enemy, EnemyRewardMode mode)
            => mode == EnemyRewardMode.TezlaJustFame
                && enemy != null && (enemy.Faction == EnemyFaction.Elementalist || enemy.Faction == EnemyFaction.DarkCrusader) ? 1 : 0;
        internal static int TakeBonus(ActionContext context, Monster enemy, EnemyDefeatSource source)
            => context != null && PreviewBonus(enemy, context.EnemyRewardMode) > 0
                ? context.EnemyRewards.Take(enemy, source) : 0;
    }
}
