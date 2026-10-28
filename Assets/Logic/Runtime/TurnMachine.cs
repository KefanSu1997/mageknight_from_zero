// MageKnightLogicProj/Runtime/TurnMachine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 控制战斗四子阶段的调度器（仅 PVE）。
    /// 使用顺序：<br/>
    ///   ① <see cref="ApplyRanged"/>（远程 / 攻城）<br/>
    ///   ② <see cref="SetBlock"/>（格挡阶段，UI/AI 调用）<br/>
    ///   ③ <see cref="ResolveBattle"/>（Assign-Damage + Melee 一次完成）
    /// </summary>
    public sealed class TurnMachine
    {
        private readonly GameState _gs;
        private int _currentBlockValue      = 0;
        private Element _currentBlockElement = Element.Physical;
        private ActionContext? _defenseContext;
        private ActionContext? _battleContext;
        private IList<Monster>? _defenseEnemies;

        public TurnMachine(GameState gs) => _gs = gs;

        /// <summary>开始本次战斗；不能从尚未结束的战斗重新开始。</summary>
        public void BeginBattle(ActionContext? ctx = null)
        {
            if (ctx?.PeacefulMomentAction == true)
                throw new InvalidOperationException("宁静时光已占用本回合行动");
            if (_gs.CurrentPhase != Phase.None && _gs.CurrentPhase != Phase.End)
                throw new InvalidOperationException("当前战斗尚未结束");
            if (_gs.CurrentPhase == Phase.End) _gs.NextPhase();
            _defenseContext = null; _defenseEnemies = null;
            _battleContext = ctx;
            _gs.NextPhase();
            if (ctx != null)
            {
                ctx.DefeatedEnemyIndices.Clear();
                ctx.EnemyRewards.BeginBattle();
                DuelActions.ClearBattle(ctx);
                NatureRevengeActions.ClearBattle(ctx);
                ctx.InBattle = true;
                ctx.CombatActionStarted = false;
                ctx.BeginBattleWounds(_gs.ActivePlayer);
                CardCombatActions.EnterPhase(ctx, Phase.Ranged);
                ctx.SharedSkills?.OnBattleStarted(_gs.ActivePlayer,ctx);
            }
        }

        /*──────────────────────────────────────────────
         * Phase-1 : 远程 / 攻城
         *────────────────────────────────────────────*/
        public void ApplyRanged(
            IList<Monster> enemies,
            IReadOnlyList<AttackAllocation> rangedColumns,
            ActionContext? ctx = null)
        {
            EnsurePhase(Phase.Ranged);
            CardCombatActions.RequireExplosiveBoltArmorResolved(ctx);

            if (ctx != null) CardCombatActions.EnterPhase(ctx, Phase.Ranged);

            var res = RangedPhaseResolver.Resolve(enemies, rangedColumns, ctx);

            CardCombatActions.ApplyRangedRewards(_gs.ActivePlayer, enemies, res, ctx);
            if (ctx != null && res.KilledIndices.Count > 0)
            {
                var killed = new HashSet<int>(res.KilledIndices);
                var survivors = Enumerable.Range(0, enemies.Count).Where(i => !killed.Contains(i))
                    .Select((original, current) => (original, current)).ToDictionary(p => p.original, p => p.current);
                ctx.RemapEnemyModifiers(survivors);
            }
            foreach (int i in res.KilledIndices.OrderByDescending(x => x))
                enemies.RemoveAt(i);

            if (ctx != null && ctx.FamePerUnitAction > 0)
            {
                _gs.ActivePlayer.Fame += ctx.FamePerUnitAction * rangedColumns.Count;
            }

            // The entire ranged/siege phase is closing, including unused power.
            ctx?.CombatPower.ConsumeAttacks(Phase.Ranged);

            // Missing real summon supply must not advance into a zero-attack defense.
            if (ctx != null) CardCombatActions.EnterPhase(ctx, Phase.Block);
            _gs.NextPhase();   // → Block
        }

        /*──────────────────────────────────────────────
         * Phase-2 : Block（由 UI / AI 在外部调用）
         *────────────────────────────────────────────*/
        public void SetBlock(int value, Element element)
        {
            EnsurePhase(Phase.Block);

            _currentBlockValue   = value;
            _currentBlockElement = element;
            _gs.ActivePlayer.LastBlockElement = element;

            _gs.NextPhase();   // → AssignDamage
            if (_battleContext != null) CardCombatActions.EnterPhase(_battleContext, Phase.AssignDamage);
        }

        /*──────────────────────────────────────────────
         * Phase-3-4 : AssignDamage + Melee（一次结算）
         *────────────────────────────────────────────*/
        public BattleResult ResolveBattle(
            IList<Monster> enemies,
            IReadOnlyList<BlockAllocation> blocks,
            IReadOnlyList<AttackAllocation> attacks,
            IList<UnitState>? units = null,
            int attackReduction = 0,
            ActionContext? ctx = null)
        {
            EnsurePhase(Phase.AssignDamage);

            // The combined API must also end the physical replacement before melee.
            // Resolving both stages against the brown defender would attack the wrong entity.
            if (ctx?.Summoning.IsActive == true)
            {
                var defense = BattleResolver.Resolve(_gs.ActivePlayer, enemies, blocks,
                    Array.Empty<AttackAllocation>(), units, attackReduction, ctx);
                _gs.NextPhase();
                CardCombatActions.EnterPhase(ctx, Phase.Melee);
                var melee = BattleResolver.Resolve(_gs.ActivePlayer, enemies, Array.Empty<BlockAllocation>(),
                    attacks, units, ctx: ctx, enemyAttacks: Array.Empty<(int Target, int Attack)>());
                _gs.NextPhase();
                UnitDamageResolver.EndBattle(_gs.ActivePlayer, ctx);
                CardCombatActions.EnterPhase(ctx, Phase.End);
                return new BattleResult(melee.AllKilled, defense.TotalWounds + melee.TotalWounds,
                    defense.FameGain + melee.FameGain)
                { KilledIndices = defense.KilledIndices.Concat(melee.KilledIndices).Distinct().ToArray() };
            }

            var res = BattleResolver.Resolve(
                _gs.ActivePlayer,
                enemies,
                blocks,
                attacks,
                units,
                attackReduction,
                ctx);

            _gs.NextPhase();                     // AssignDamage → Melee
            if (ctx != null) CardCombatActions.EnterPhase(ctx, Phase.Melee);
            _gs.NextPhase();                     // Melee → End (both resolved above)
            if (ctx != null)
            {
                UnitDamageResolver.EndBattle(_gs.ActivePlayer, ctx);
                CardCombatActions.EnterPhase(ctx, Phase.End);
            }
            return res;
        }

        /// <summary>先结算格挡与伤害，保留原卡产生的攻击供攻击阶段选择目标。</summary>
        public BattleResult ResolveDefense(IList<Monster> enemies, IReadOnlyList<BlockAllocation> blocks,
            ActionContext ctx, IReadOnlyList<UnitDamageAllocation>? damageAssignments = null)
        {
            EnsurePhase(Phase.AssignDamage);
            var result = BattleResolver.Resolve(_gs.ActivePlayer, enemies, blocks, Array.Empty<AttackAllocation>(),
                ctx: ctx, damageAssignments: damageAssignments);
            ctx.CombatPower.ConsumeBlocks();
            _defenseContext = ctx; _defenseEnemies = enemies;
            _gs.NextPhase();
            CardCombatActions.EnterPhase(ctx, Phase.Melee);
            return result;
        }

        /// <summary>伤害已结算，按实际原卡贡献攻击；不会再次承伤或重复击败同一目标。</summary>
        public CardCombatActions.Result Attack(IReadOnlyList<int> targets, ActionContext ctx, IReadOnlyList<AttackProfile> selected = null)
        {
            EnsurePhase(Phase.Melee);
            RequireDefenseContext(ctx);
            if (targets != null && targets.Any(ctx.DefeatedEnemyIndices.Contains))
                throw new InvalidOperationException("目标已经被击败");
            var result = CardCombatActions.Attack(_gs.ActivePlayer, ctx, _defenseEnemies!, targets, Phase.Melee, selected);
            return result;
        }

        /// <summary>玩家确认不再攻击后结束战斗；未使用的奖励失效。</summary>
        public void FinishBattle(ActionContext ctx)
        {
            EnsurePhase(Phase.Melee);
            RequireDefenseContext(ctx);
            UnitDamageResolver.EndBattle(_gs.ActivePlayer, ctx);
            _gs.NextPhase();
            CardCombatActions.EnterPhase(ctx, Phase.End);
        }

        private void RequireDefenseContext(ActionContext ctx)
        {
            if (!ReferenceEquals(ctx, _defenseContext) || _defenseEnemies == null)
                throw new InvalidOperationException("必须沿用本场格挡和伤害结算状态");
        }

        /*────────────────────────────────────────────*/
        private void EnsurePhase(Phase need)
        {
            if (_gs.CurrentPhase != need)
                throw new InvalidOperationException(
                    $"必须在 {need} 阶段调用，当前：{_gs.CurrentPhase}");
        }
    }
}
