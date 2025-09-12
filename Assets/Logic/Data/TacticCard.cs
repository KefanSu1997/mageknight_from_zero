using MK.Logic.Runtime;
using MK.Logic.Core;

namespace MK.Logic.Data
{
    /// <summary>
    /// 戰術牌資料結構，包含順序編號與手牌加成。
    /// 僅供流程控制使用，實際效果可在 UI 層實現。
    /// </summary>
    public sealed record TacticCard(string Id, DayPart Part, int Number, int HandBonus);
}
