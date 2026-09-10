using System;

[Serializable]
public sealed class CardVerificationCheck
{
    public string key;
    public string expected;
    public string actual;
    public bool passed;
}
