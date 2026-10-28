using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        private static bool IsOutsideUnitAbility(UnitAbility ability) => ability.Kind switch
        {
            UnitAbilityKind.Movement or UnitAbilityKind.Influence or UnitAbilityKind.MovementOrInfluence
                or UnitAbilityKind.GainMana or UnitAbilityKind.GainToken or UnitAbilityKind.GainCrystal
                or UnitAbilityKind.GainChosenTokens or UnitAbilityKind.Healing or UnitAbilityKind.ReadyUnit
                or UnitAbilityKind.AllUnitResistance or UnitAbilityKind.ScoutToken => true,
            _ => false
        };

        // These are printed choices, not a promise that their costs/targets/readiness are valid.
        public int[] OutsideUnitAbilities(UnitState unit)
        {
            if (unit == null || !Player.Units.Contains(unit) || unit.IsDestroyed || Context == null
                || !Game.HasActiveTurn || Game.IsEndingTurn || Context.InBattle) return Array.Empty<int>();
            return Enumerable.Range(0,unit.Card.Actions.Length).Where(i => IsOutsideUnitAbility(unit.Card.Actions[i])
                && (!(unit.Card.Actions[i].Kind == UnitAbilityKind.Movement || unit.Card.Actions[i].MovementPhaseOnly)
                    || !Context.TurnActionCommitted)).ToArray();
        }

        public void UseOutsideUnit(UnitState unit, int abilityIndex, int option = 0,
            UnitState unitTarget = null, ManaColor[] manaColors = null, ScoutTarget scoutTarget = null)
            => UseOutsideUnitCore(unit, abilityIndex, option, unitTarget, manaColors, scoutTarget);

        public void UseOutsideUnitWithBorrowedPayment(DeedCard physical, ManaSkillPaymentSelection payment,
            UnitState unit, int abilityIndex, int option = 0, UnitState unitTarget = null,
            ManaColor[] manaColors = null, ScoutTarget scoutTarget = null)
        {
            if (physical == null) throw new ArgumentNullException(nameof(physical));
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            UseOutsideUnitCore(unit, abilityIndex, option, unitTarget, manaColors, scoutTarget, physical, payment);
        }

        private void UseOutsideUnitCore(UnitState unit, int abilityIndex, int option,
            UnitState unitTarget, ManaColor[] manaColors, ScoutTarget scoutTarget,
            DeedCard physical = null, ManaSkillPaymentSelection payment = null)
        {
            RequireOutsideBattle();
            // Borrow authorization is established inside the typed payment adapter before its rules guard.
            if (physical == null) RepeatedEffectResolution.RequireCompleted(Context);
            if (!OutsideUnitAbilities(unit).Contains(abilityIndex))
                throw new InvalidOperationException("请选择实际自有部队在当前战斗外时机可用的原能力");
            var ability = unit.Card.Actions[abilityIndex];
            bool movement = ability.Kind == UnitAbilityKind.Movement
                || ability.Kind == UnitAbilityKind.MovementOrInfluence && option == 0;
            if (movement && Context.TurnActionCommitted)
                throw new InvalidOperationException("移动能力只能在本回合行动前发动");
            if (unitTarget != null && ability.Kind != UnitAbilityKind.ReadyUnit)
                throw new InvalidOperationException("仅部队重整能力需要部队目标，治疗点随后另选实际创伤消费");
            if (physical == null)
                CardUnitActions.Use(Player,Context,unit,abilityIndex,option,unitTarget:unitTarget,
                    manaColors:manaColors,scoutTarget:scoutTarget);
            else
                ManaSkillPaymentActions.ExecuteBorrowedUnit(Player,Context,physical,payment,unit,abilityIndex,option,
                    unitTarget:unitTarget,manaColors:manaColors,scoutTarget:scoutTarget);
        }
    }
}
