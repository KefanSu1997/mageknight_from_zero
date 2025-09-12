namespace MK.Logic.Data
{
    /// <summary>
    /// 特殊地點的靜態資料結構。
    /// </summary>
    public sealed record PlaceData(
        string Id,
        string SiteName,
        string FlipEffect,
        string OngoingEffect,
        string ActionText,
        string RewardText
    );
}
