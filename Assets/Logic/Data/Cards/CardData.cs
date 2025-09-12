namespace MK.Logic.Data.Cards
{
    using MK.Logic.Core;

    /// <summary>
    /// 所有卡牌資料的共同基底，用於轉換 ScriptableObject。
    /// </summary>
    public abstract record CardData(
        string Id,
        string Name,
        CardSet Set,
        string ImagePath,
        string EnImagePath
    );
}
