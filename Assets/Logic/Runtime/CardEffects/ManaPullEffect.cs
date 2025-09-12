using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力征引：基礎額外使用骰子且黑色可通用，強效轉換兩顆骰子為標記。
    /// </summary>
    public sealed class ManaPullEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaPullEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.ExtraManaDice += 1;
                ctx.BlackManaWild = true;
            }
            else
            {
                ManaColor c1 = (ManaColor)(option & 0xFF);
                ManaColor c2 = (ManaColor)((option >> 8) & 0xFF);
                player.Mana.AddToken(c1, 1);
                player.Mana.AddToken(c2, 1);
            }
        }
    }
}
