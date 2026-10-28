using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public BattleCardChoice[] OfferedSpellChoices(SpellCard offered) => offered == null || Context == null
            ? Array.Empty<BattleCardChoice>()
            : BattleCardChoice.For(offered.Id).Where(c => c.Allows(Context.InBattle ? Battle.Phase : Phase.End)).ToArray();

        /// <summary>Spend an actual turn permission on a supplied spell, retaining its supply identity.</summary>
        public void PlayOfferedSpell(SpellCard offered, string choiceId, int target = -1,
            PveDeedSelection inputs = null, int attackIndex = 0)
        {
            if (!Game.HasActiveTurn || Game.IsEndingTurn || Context == null)
                throw new InvalidOperationException("请在自己的实际回合使用供应施法许可");
            int index = Game.Globals.Spells.Offer.FindIndex(c => ReferenceEquals(c, offered));
            if (index < 0 || !ReferenceEquals(Context.SpellSupply, Game.Globals.Spells))
                throw new InvalidOperationException("请选择当前实际法术供应中的原牌");
            var choice = OfferedSpellChoices(offered).SingleOrDefault(c => c.Id == choiceId)
                ?? throw new InvalidOperationException("此供应法术效果不能在当前阶段使用");
            ValidateWorldSpell(offered.Id,choice,inputs);
            if (choice.Target != BattleCardTarget.None && (!Context.InBattle || Battle == null
                || target < 0 || target >= Battle.Enemies.Count || Context.DefeatedEnemyIndices.Contains(target)))
                throw new InvalidOperationException("请选择实际存活的敌人目标");
            if (choice.Target == BattleCardTarget.EnemyAttack && (attackIndex < 0
                || attackIndex >= Battle.Enemies[target].AttackCount || Battle.IsBlocked(target, attackIndex)))
                throw new InvalidOperationException("请选择尚未格挡的实际敌人攻击");
            var unit = inputs?.Unit;
            if (unit != null && (!Player.Units.Contains(unit) || unit.IsDestroyed))
                throw new InvalidOperationException("请选择实际自有部队");
            var oldEnemy = Context.SelectedEnemy; int oldAttack = Context.SelectedEnemyAttackIndex;
            var oldMana = Context.SelectedActionManaColor; var oldUnit = Context.TargetUnit;
            var oldMany = Context.ReadyUnitTargets;
            try
            {
                Context.SelectedActionManaColor = choice.ActionMana;
                Context.TargetUnit = unit;
                Context.ReadyUnitTargets = unit == null ? new List<UnitState>() : new List<UnitState> { unit };
                if (choice.Target == BattleCardTarget.EnemyAttack)
                {
                    Context.SelectedEnemy = Battle.Enemies[target];
                    Context.SelectedEnemyAttackIndex = attackIndex;
                }
                // This descriptor only selects the original input schema. It never enters any card zone.
                var descriptor = new DeedCard(offered.Id, CardType.Spell);
                PveDeedInputs.Execute(Player, Context, descriptor, choice, inputs, option =>
                    CardSpellActions.PlayFromOffer(Player, Context, index, choice.Enhanced,
                        choice.Target == BattleCardTarget.EnemyIndex ? target : option, _spells, _actions));
                AttachTunnelBattle();
            }
            finally
            {
                Context.SelectedEnemy = oldEnemy; Context.SelectedEnemyAttackIndex = oldAttack;
                Context.SelectedActionManaColor = oldMana; Context.TargetUnit = oldUnit;
                Context.ReadyUnitTargets = oldMany;
            }
        }
    }
}
