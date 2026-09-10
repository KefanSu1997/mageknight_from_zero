using System;

[Serializable]
public sealed class CardVerificationResult
{
    public string id;
    public string cardId;
    public string title;
    public string status;
    public string exception;
    public string executionPath;
    public string printedEffect;
    public string artwork;
    public string[] limitations;
    public CardVerificationValue[] before;
    public CardVerificationValue[] after;
    public CardVerificationCheck[] checks;
}
