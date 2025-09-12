namespace MK.Logic.Data.Cards
{
    using MK.Logic.Core;

    /// <summary>
    /// 神器或物品牌資料。
    /// </summary>
    public sealed record ItemCardData(
        string Id,
        string Name,
        CardSet Set,
        string ImagePath,
        string EnImagePath,
        string ItemType,
        string AssignEffect,
        string OncePerRound
    ) : CardData(Id, Name, Set, ImagePath, EnImagePath);
}
