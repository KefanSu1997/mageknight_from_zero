namespace MK.Logic.Data
{
    using MK.Logic.Core;

    /// <summary>
    /// 玩家行動牌的基本結構，僅包含識別與類型。
    /// </summary>
    public sealed record DeedCard(string Id, CardType Type);
}
