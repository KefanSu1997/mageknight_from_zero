using MK.Logic.Core;

namespace MK.Logic.Data
{
    public enum UnitAbilityKind { Attack, Block, AttackOrBlock, Movement, Influence }

    /// <summary>One selectable printed ability. Costs and choices belong to this ability, not the unit as a whole.</summary>
    public sealed record UnitAbility(string PrintedRule, UnitAbilityKind Kind, int Value,
        Element Element, AttackType AttackType, ManaColor[] Cost, int SelfWounds = 0);
}
