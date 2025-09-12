namespace MK.Logic.Data.Cards
{
    using MK.Logic.Core;

    /// <summary>
    /// 普通與高級行動牌資料。
    /// </summary>
    public sealed record ActionCardData(
        string Id,
        string Name,
        CardSet Set,
        string ImagePath,
        string EnImagePath,
        string BaseEffect,
        string EnhancedEffect,
        ManaColor[] RequiredCrystals
    ) : CardData(Id, Name, Set, ImagePath, EnImagePath);
}
