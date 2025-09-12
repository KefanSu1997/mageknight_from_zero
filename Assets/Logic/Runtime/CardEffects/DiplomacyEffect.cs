using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 外交：影響力可作為格擋使用，強效可指定火或冰元素。
    /// option 參數 0 表示寒冰，1 表示火焰。
    /// </summary>
    public sealed class DiplomacyEffect : ICardEffect
    {
        private readonly bool _enh;
        public DiplomacyEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.InfluencePool += 2;
                ctx.InfluenceAsBlock = true;
                ctx.InfluenceBlockElement = Element.Physical;
            }
            else
            {
                ctx.InfluencePool += 4;
                ctx.InfluenceAsBlock = true;
                ctx.InfluenceBlockElement = option == 0 ? Element.Ice : Element.Fire;
            }
        }
    }
}
