using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 外交：影響力可作為格擋使用，強效可指定火或冰元素。
    /// option 參數 0 表示寒冰，1 表示火焰。
    /// </summary>
    public sealed class DiplomacyEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public DiplomacyEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh && (option < 0 || option > 1))
                throw new InvalidOperationException("外交强化必须选择冰格挡或火格挡");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
