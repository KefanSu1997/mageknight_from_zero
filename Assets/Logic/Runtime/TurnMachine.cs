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

        public TurnMachine(GameState gs) => _gs = gs;

        /*──────────────────────────────────────────────
         * Phase-1 : 远程 / 攻城
         *────────────────────────────────────────────*/
        public void ApplyRanged(
            IList<Monster> enemies,
            IReadOnlyList<AttackAllocation> rangedColumns,
            ActionContext? ctx = null)
        {
            EnsurePhase(Phase.Ranged);

            var res = RangedPhaseResolver.Resolve(enemies, rangedColumns, ctx);

            CardCombatActions.ApplyRangedRewards(_gs.ActivePlayer, enemies, res, ctx);
            foreach (int i in res.KilledIndices.OrderByDescending(x => x))
                enemies.RemoveAt(i);

            if (ctx != null && ctx.FamePerUnitAction > 0)
            {
                _gs.ActivePlayer.Fame += ctx.FamePerUnitAction * rangedColumns.Count;
            }

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

            var res = BattleResolver.Resolve(
                _gs.ActivePlayer,
                enemies,
                blocks,
                attacks,
                units,
                attackReduction,
                ctx);

            _gs.NextPhase();                     // → End
            return res;
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
