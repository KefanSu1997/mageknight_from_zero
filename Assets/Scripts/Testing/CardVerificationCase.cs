using System;

[Serializable]
public sealed class CardVerificationCase
{
    public string id;
    public bool basicAcceptance;
    public bool crystalMasteryScenario;
    public string cardId;
    public string title;
    public string batch;
    public bool enhanced;
    public int option;
    public string effectChoice;
    public string followup;
    public bool followupEnhanced;
    public string expectedException;
    public string combatPhase;
    public string turnMachineStartPhase;
    public CardVerificationEnemy[] enemies;
    public string[] limitations;
    public CardVerificationValue[] setup;
    public CardVerificationValue[] expected;
    public CardVerificationOperation[] operations;
    public int[] unitLevels;
    public string[] unitIds;
    public string skillPoolId;
    public int[] skillIndices;
    public int[] skillOfferIndices;
    public int headbandSkillOption;
    public int unitWounds;
    public int[] initialUnitWounds;
    public int[] readyUnitTargets;
    public CardVerificationOtherPlayer[] otherPlayers;
    public int[] mindReadDiscardIndices;
    public int mindStealPlayerIndex = -1;
    public CardVerificationUnitTarget[] exhaustTargets;
    public bool unitInitiallyReady;
    public int[] initialReadyUnitIndices;
    public bool unitDestroyed;
    public bool omitUnitTarget;
    public bool foreignUnitTarget;
    public string destinationTerrain;
    public bool omitMovementTarget;
    public int[] manaRolls;
    public int[] manaSourceRolls;
    public string[] manaSourceFaces;
    public int[] manaTransformIndices;
    public string[] bloodRitualColors;
    public string bloodRitualCrystalColor;
    public string selectedActionManaColor;
    public int selectedEnemyIndex = -1;
    public int selectedEnemyAttackIndex;
    public int[] manaPowerBonusChoices;
    public int manaPaymentDieIndex = -1;
    public string[] extraHandCards;
    public int[] discardIndices;
    public bool foreignDiscard;
    public int removeIndex = 1;
    public bool foreignRemove;
    public CardVerificationRepeatedSelection[] overloadSelections;
    public bool overloadStepwise;
    public int discardIndex = 1;
    public string[] spellOffer;
    public string[] advancedOffer;
    public string[] advancedReserve;
    public string[] spellReserve;
    public bool useRoundEngine;
    public bool useTurnEngine;
    public int[] exploreTileIds;
    public string[] artifactSupplyIds;
    public bool turnPowerExpiry;
    public string[] recruitOffer;
    public int disbandUnitIndex = -1;
    public bool foreignDisband;
    public int initialHandCount = -1;
    public int initialDrawCount = 10;
    public string[] initialDrawCards;
    public string[] initialDiscardCards;
    public int[] recoverDiscardIndices;
    public bool foreignRecovery;
    public CardVerificationPlace[] mapPlaces;
    public CardVerificationTerrain[] mapTerrains;
    public string[] enemySupplyIds;
    public int ongoingBlackMana;
    public bool playFromHand;
    public CardVerificationHex[] travelPath;
    public int[] spellTargets;
    public int revealedDistance = 2;
}

[Serializable]
public sealed class CardVerificationRepeatedSelection
{
    public int option;
    public int removeIndex = -1;
    public int discardIndex = -1;
    public bool foreignRemove;
    public bool foreignDiscard;
}

[Serializable]
public sealed class CardVerificationHex { public int q; public int r; }

[Serializable]
public sealed class CardVerificationOtherPlayer
{
    public CardVerificationValue[] crystals;
    public CardVerificationValue[] tokens;
    public string[] extraHandCards;
    public int q;
    public int r;
    public string[] unitIds;
    public int[] exhaustedIndices;
    public int[] destroyedIndices;
}

[Serializable]
public sealed class CardVerificationUnitTarget
{
    public int playerIndex;
    public int unitIndex;
    public bool foreign;
}

[Serializable]
public sealed class CardVerificationPlace
{
    public string ruinToken;
    public int q;
    public int r;
    public string type;
    public bool enemy;
    public CardVerificationEnemy enemyDefinition;
    public bool guardFromSupply;
    public string[] guardColors;
    public string cityColor;
    public int[] cityShields;
    public bool guardNeedsReplacement;
    public int ownerId = -1;
}

[Serializable]
public sealed class CardVerificationTerrain { public int q; public int r; public string terrain; }

[Serializable]
public sealed class CardVerificationOperation
{
    public string selectedActionManaColor;
    public int[] manaPowerBonusChoices;
    public int playerIndex;
    public CardVerificationRepeatedSelection repeatedSelection;
    public int abilityChoice;
    public string kind;
    public string cardId;
    public int q;
    public int r;
    public int targetIndex;
    public int attackIndex;
    public int[] unitTargets;
    public int[] attackTargets;
    public string recruitLocation;
    public bool enhanced;
    public int option;
    public bool refill;
}
