namespace MK.Logic.Data.Cards
{
    using MK.Logic.Core;

    /// <summary>
    /// 法術牌資料，包含上下半張描述。
    /// </summary>
    /// <summary>法術雙面半張的數據。</summary>
    public sealed record HalfSpell(string Name, string Effect, ManaColor[] ManaCost);

    public sealed record SpellCardData(
        string Id,
        string Name,
        CardSet Set,
        string ImagePath,
        string EnImagePath,
        ManaColor ManaColor,
        HalfSpell Top,
        HalfSpell Bottom
    ) : CardData(Id, Name, Set, ImagePath, EnImagePath);
}
