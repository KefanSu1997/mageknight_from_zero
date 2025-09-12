using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 純淨魔法：支付選定顏色魔力以獲得對應數值。
    /// option 為顏色索引 0=Red 1=Blue 2=Green 3=White。
    /// </summary>
    public sealed class PureMagicEffect : ICardEffect
    {
        private readonly bool _enh;
        public PureMagicEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var color = (ManaColor)System.Math.Clamp(option, 0, 3);
            if (!PayColor(player, color)) return;

            int val = _enh ? 7 : 4;
            switch (color)
            {
                case ManaColor.Green:
                    ctx.MovementPool += val;
                    break;
                case ManaColor.White:
                    ctx.InfluencePool += val;
                    break;
                case ManaColor.Blue:
                    ctx.BlockPool += val;
                    break;
                case ManaColor.Red:
                    ctx.MeleePool += val;
                    break;
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

