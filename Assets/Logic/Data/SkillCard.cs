namespace MK.Logic.Data
{
    /// <summary>
    /// 英雄技能牌的簡易描述結構。
    /// </summary>
    using MK.Logic.Runtime.CardEffects;

    /// <summary>
    /// 英雄技能牌的簡易描述結構。
    /// </summary>
    public enum SkillFrequency { Unspecified, OncePerTurn, OncePerRound, ManualFlip }

    public sealed record SkillCard(string Id, ICardEffect Effect, bool Reusable,
        string SourceId = "", string PrintedRule = "", SkillFrequency Frequency = SkillFrequency.Unspecified,
        bool? IsInteractive = null);
}
