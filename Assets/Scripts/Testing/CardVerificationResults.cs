using System;

[Serializable]
public sealed class CardVerificationResults
{
    public string startedAt;
    public string finishedAt;
    public string status;
    public string batch;
    public CardVerificationResult[] cases;
}
