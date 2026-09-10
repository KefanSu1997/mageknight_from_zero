using System;

[Serializable]
public sealed class CardVerificationCase
{
    public string id;
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
    public CardVerificationEnemy[] enemies;
    public string[] limitations;
    public CardVerificationValue[] setup;
    public CardVerificationValue[] expected;
    public CardVerificationOperation[] operations;
    public int[] unitLevels;
    public int unitWounds;
    public bool unitInitiallyReady;
    public bool unitDestroyed;
    public string destinationTerrain;
    public bool omitMovementTarget;
    public int[] manaRolls;
}

[Serializable]
public sealed class CardVerificationOperation
{
    public string kind;
    public int q;
    public int r;
    public int targetIndex;
}
