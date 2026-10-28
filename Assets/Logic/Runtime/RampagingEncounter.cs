using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;
using Monster = MK.Logic.Data.Monster;

namespace MK.Logic.Runtime
{
    /// <summary>Moving between two hexes adjacent to a rampaging enemy starts its actual combat.</summary>
    public sealed class RampagingEncounter
    {
        private readonly (PlaceState site, Monster enemy)[] _targets;
        private readonly List<Monster> _defeated = new();
        public TurnMachine Machine { get; }
        internal GameState State { get; }

        private RampagingEncounter(PlayerState player, ActionContext context, PlaceState[] sites, bool deferBattle = false)
        {
            _targets = sites.Select(s => (s, s.Enemy)).ToArray();
            var game = new GameState();
            State = game;
            game.Players.Add(player);
            Machine = new TurnMachine(game);
            context.Enemies = _targets.Select(t => t.enemy).ToList();
            if(!deferBattle)
            {
                Machine.BeginBattle(context);
                context.TurnActionCommitted = true;
            }
        }

        private static bool Adjacent(AxialCoord a, AxialCoord b)
        {
            int q = a.Q - b.Q, r = a.R - b.R;
            return (Math.Abs(q) + Math.Abs(r) + Math.Abs(q + r)) / 2 == 1;
        }

        internal static RampagingEncounter Challenge(PlayerState player, MapState map, AxialCoord target, ActionContext context)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (context.InBattle || context.TurnActionCommitted || context.RestedThisTurn || context.PeacefulMomentAction
                || !map.Placed.ContainsKey(target) || !Adjacent(player.Position, target)
                || !map.Places.TryGetValue(target, out var site)
                || (site.Type != PlaceType.OrcRampaging && site.Type != PlaceType.Dragon)
                || site.Enemy == null || context.EnemySupply == null || !context.EnemySupply.IsInUse(site.Enemy))
                throw new InvalidOperationException("只能挑战相邻地点中来自实际供给的肆虐敌人，且本回合仍可行动");
            context.RampagingBattle = new RampagingEncounter(player, context, new[] { site });
            context.MovementPool = context.InfluencePool = 0;
            return context.RampagingBattle;
        }

        internal static void AfterMove(PlayerState player, MapState map, AxialCoord from, AxialCoord to, ActionContext context, bool deferBattle = false)
        {
            if (context.IgnoreRampaging) return;
            var sites = map.Places.Where(p => (p.Value.Type == PlaceType.OrcRampaging || p.Value.Type == PlaceType.Dragon)
                && p.Value.Enemy != null && Adjacent(from, p.Key) && Adjacent(to, p.Key)).Select(p => p.Value).ToArray();
            if (sites.Length > 0) context.RampagingBattle = new RampagingEncounter(player, context, sites, deferBattle);
        }

        internal void RecordDefeat(Monster enemy)
        {
            if (_targets.Any(t => ReferenceEquals(t.enemy, enemy)) && !_defeated.Any(e => ReferenceEquals(e, enemy)))
                _defeated.Add(enemy);
        }

        internal void Complete(PlayerState player, ActionContext context)
        {
            foreach (var target in _targets.Where(t => _defeated.Any(e => ReferenceEquals(e, t.enemy))))
            {
                if (!ReferenceEquals(target.site.Enemy, target.enemy))
                    throw new InvalidOperationException("肆虐地点的实际敌人已改变");
                if (context.EnemySupply?.IsInUse(target.enemy) == true) context.EnemySupply.Discard(target.enemy);
                target.site.Challenge(player, true);
            }
            context.RampagingBattle = null;
        }
    }
}
