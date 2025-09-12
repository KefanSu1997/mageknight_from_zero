using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 獻祭 / 犧牲：轉化手牌為魔晶，或以魔晶成對換取攻擊。
    /// option 低8位與高8位分別為兩種顏色（強效）。
    /// </summary>
    public sealed class SacrificeEffect : ICardEffect
    {
        private readonly bool _enh;
        public SacrificeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                player.Mana.AddCrystal(ManaColor.Red, 1);
                int cnt = 0;
                foreach (var c in ctx.CardsToDiscard.Take(3))
                {
                    if (player.Deck.RemoveFromHand(c))
                    {
                        player.Mana.AddCrystal(ManaColor.Red, 1);
                        cnt++;
                    }
                }
                ctx.CardsToDiscard.Clear();
            }
            else
            {
                ManaColor c1 = (ManaColor)(option & 0xFF);
                ManaColor c2 = (ManaColor)((option >> 8) & 0xFF);
                player.Mana.Crystals.TryGetValue(c1, out int n1);
                player.Mana.Crystals.TryGetValue(c2, out int n2);
                int pair = System.Math.Min(n1, n2);
                if (pair == 0) return;
                if (c1 == ManaColor.Green)
                    ctx.MeleePool += 4 * pair;
                else if (c1 == ManaColor.White)
                    ctx.RangedPool += 6 * pair;
                player.Mana.Crystals[c1] = n1 - pair;
                if (player.Mana.Crystals[c1] == 0) player.Mana.Crystals.Remove(c1);
                player.Mana.Crystals[c2] = n2 - pair;
                if (player.Mana.Crystals[c2] == 0) player.Mana.Crystals.Remove(c2);
                player.Mana.AddToken(c1, pair);
                player.Mana.AddToken(c2, pair);
            }
        }
    }
}
