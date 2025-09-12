namespace MK.Logic.Data
{
    using MK.Logic.Core;
    using System.Collections.Generic;

    /// <summary>
    /// 表示一項行動所需的法力成本。
    /// </summary>
    public sealed record ManaCost(Dictionary<Element, int> Need);
}
