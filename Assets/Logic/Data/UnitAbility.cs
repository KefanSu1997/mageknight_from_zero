using MK.Logic.Core;

namespace MK.Logic.Data
{
    public enum UnitAbilityKind { Attack, Block, AttackOrBlock, Movement, Influence, FreezeArmor, GainMana, GainToken, Healing, ReadyUnit, AllUnitResistance, GainChosenTokens, TransformAttacks, GainCrystal, StopUnfortifiedEnemy, StopEnemy, DestroyBlockedEnemy, ReduceEnemyArmor, RemoveEnemyDefenses, WeakenEnemyAttack, InterceptEnemyAttack, MovementOrInfluence, ScoutToken }

    /// <summary>One selectable printed ability. Costs and choices belong to this ability, not the unit as a whole.</summary>
    public sealed record UnitAbility(string PrintedRule, UnitAbilityKind Kind, int Value,
        Element Element, AttackType AttackType, ManaColor[] Cost, int SelfWounds = 0, int ReputationChange = 0,
        bool DoubleAgainstSwift = false, ManaColor? GrantedMana = null, bool RedBlueBoost = false,
        TerrainType[] MovementReductionTerrains = null, int MovementReduction = 0,
        bool RemoveTargetFortifications = false, bool RemoveTargetResistances = false,
        int InteractionReputationChange = 0, int OtherUnitAttackBonus = 0,
        ManaColor? AttunementColor = null, int AttunementBonus = 0,
        int ExplorationRange = 1, bool MovementPhaseOnly = false);
}
