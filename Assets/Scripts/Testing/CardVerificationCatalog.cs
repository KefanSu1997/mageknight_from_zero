using System;
using UnityEngine;

/// <summary>Only references existing cards and their original printed artwork.</summary>
public sealed class CardVerificationCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public CardSO source;
        public Sprite artwork;
    }

    public Entry[] cards;
    public TextAsset cases;
}
