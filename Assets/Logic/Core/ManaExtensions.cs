using MK.Logic.Runtime;
namespace MK.Logic.Core
{
    /// <summary>
    /// 法力顏色相關的擴充方法，依 <see cref="resources/principle/manaColor.md"/> 確定對應關係。
    /// </summary>
    public static class ManaExtensions
    {
        /// <summary>
        /// 判斷此顏色是否為四種基本色（紅、藍、綠、白）。
        /// </summary>
        public static bool IsBasic(this ManaColor c) =>
            c is ManaColor.Red or ManaColor.Blue or ManaColor.Green or ManaColor.White;

        /// <summary>
        /// 轉換顏色至戰鬥元素。僅紅、藍對應火與冰，其餘視為物理元素。
        /// </summary>
        public static Element ToElement(this ManaColor c) => c switch
        {
            ManaColor.Red => Element.Fire,
            ManaColor.Blue => Element.Ice,
            _ => Element.Physical
        };

        /// <summary>
        /// 檢查此顏色在當前日夜是否為合法法力。
        /// 金色僅在白晝可用，黑色僅在黑夜可用。
        /// </summary>
        public static bool IsManaLegal(this ManaColor c, DayPart part) => c switch
        {
            ManaColor.Gold => part == DayPart.Day,
            ManaColor.Black => part == DayPart.Night,
            _ => true
        };

        /// <summary>
        /// 判斷此顏色是否可作為通配色。只有白晝的金色成立。
        /// </summary>
        public static bool ActsAsWildcard(this ManaColor c, DayPart part) =>
            c == ManaColor.Gold && part == DayPart.Day;

        /// <summary>
        /// 施放法術強效所需的黑色檢查：夜間、主色為基本色且額外支付黑色。
        /// </summary>
        public static bool CanPaySpellStrong(ManaColor main, ManaColor? extra, DayPart part) =>
            part == DayPart.Night && extra == ManaColor.Black && main.IsBasic();

        /// <summary>
        /// 由戰鬥元素取得對應顏色。非火／冰皆映射為白色以方便計算。
        /// </summary>
        public static ManaColor ToColor(this Element e) => e switch
        {
            Element.Fire => ManaColor.Red,
            Element.Ice => ManaColor.Blue,
            _ => ManaColor.White
        };
    }
}
