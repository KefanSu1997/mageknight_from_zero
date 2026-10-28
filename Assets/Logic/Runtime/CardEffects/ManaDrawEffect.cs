using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力汲取：基礎效果增加可用骰子，強效轉換骰子並獲得標記。
    /// </summary>
    public sealed class ManaDrawEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaDrawEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh) CardManaActions.ValidateTransform(ctx, 1, new[] { (ManaColor)option }, player);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.ExtraManaDice += 1;
            }
            else
            {
                var color = (ManaColor)option;
                CardManaActions.Transform(player, ctx, new[] { color }, 2);
            }
        }
    }
}
