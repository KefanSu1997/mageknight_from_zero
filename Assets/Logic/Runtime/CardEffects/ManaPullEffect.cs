using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力征引：基礎額外使用骰子且黑色可通用，強效轉換兩顆骰子為標記。
    /// </summary>
    public sealed class ManaPullEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaPullEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh) CardManaActions.ValidateTransform(ctx, 2,
                new[] { (ManaColor)(option & 0xFF), (ManaColor)((option >> 8) & 0xFF) }, player);
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.ExtraManaDice += 1;
                ctx.ExtraBlackManaDice += 1;
            }
            else
            {
                ManaColor c1 = (ManaColor)(option & 0xFF);
                ManaColor c2 = (ManaColor)((option >> 8) & 0xFF);
                CardManaActions.Transform(player, ctx, new[] { c1, c2 }, 1);
            }
        }
    }
}
