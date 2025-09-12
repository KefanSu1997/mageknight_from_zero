using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 鮮血儀式：自我受創獲取魔力，強化時可轉換為水晶。
    /// option 指定標記顏色索引 0-5。
    /// </summary>
    public sealed class BloodRitualEffect : ICardEffect
    {
        private readonly bool _enh;
        public BloodRitualEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var color = (ManaColor)System.Math.Clamp(option, 0, 5);
            player.Wounds += 1;

            if (!_enh)
            {
                player.Mana.AddCrystal(ManaColor.Red, 1);
                player.Mana.AddToken(color, 1);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                    player.Mana.AddToken(color, 1);

                // 嘗試支付同色魔力換取晶體
                if (color <= ManaColor.White &&
                    PayColor(player, color))
                {
                    player.Mana.AddCrystal(color, 1);
                }
            }
        }

        private static bool PayColor(PlayerState player, ManaColor color)
        {
            if (player.Mana.Tokens.TryGetValue(color, out int t) && t > 0)
            {
                if (--t == 0) player.Mana.Tokens.Remove(color); else player.Mana.Tokens[color] = t;
                return true;
            }
            if (player.Mana.Crystals.TryGetValue(color, out int c) && c > 0)
            {
                if (--c == 0) player.Mana.Crystals.Remove(color); else player.Mana.Crystals[color] = c;
                return true;
            }
            return false;
        }
    }
}

