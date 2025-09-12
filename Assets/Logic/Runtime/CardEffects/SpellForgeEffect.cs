using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 法術熔爐：從法術市場獲取相應顏色的魔晶。
    /// option 低8位與高8位表示兩種顏色索引。
    /// </summary>
    public sealed class SpellForgeEffect : ICardEffect
    {
        private readonly bool _enh;
        public SpellForgeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ManaColor c = (ManaColor)System.Math.Clamp(option & 0xFF, 0, 3);
                player.Mana.AddCrystal(c, 1);
            }
            else
            {
                ManaColor c1 = (ManaColor)System.Math.Clamp(option & 0xFF, 0, 3);
                ManaColor c2 = (ManaColor)System.Math.Clamp((option >> 8) & 0xFF, 0, 3);
                player.Mana.AddCrystal(c1, 1);
                if (c2 != c1)
                    player.Mana.AddCrystal(c2, 1);
            }
        }
    }
}
