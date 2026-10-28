using MK.Logic.Runtime;
using MK.Logic.Core;

namespace MK.Logic.Data
{
    /// <summary>
    /// 戰術牌資料結構，包含順序編號與手牌加成。
    /// 原战术由OriginalTactics提供，效果由正式TacticActions和GameEngine执行。
    /// </summary>
    public sealed record TacticCard(string Id, DayPart Part, int Number, int HandBonus);
}
