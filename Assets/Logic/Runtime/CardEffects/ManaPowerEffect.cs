using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔晶力量：基礎獲得缺少顏色的晶體，強效依擁有套數獲得額外效果。
    /// option 0移動 1治療 2抽牌
    /// </summary>
    public sealed class ManaPowerEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaPowerEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                var color = (ManaColor)System.Math.Clamp(option, 0, 3);
                if (player.Mana.Crystals.GetValueOrDefault(color) == 0)
                    player.Mana.AddCrystal(color, 1);
            }
            else
            {
                int sets = System.Math.Min(System.Math.Min(
                        player.Mana.Crystals.GetValueOrDefault(ManaColor.Red),
                        player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue)),
                    System.Math.Min(
                        player.Mana.Crystals.GetValueOrDefault(ManaColor.Green),
                        player.Mana.Crystals.GetValueOrDefault(ManaColor.White)));
                switch (option)
                {
                    case 0:
                        ctx.MovementPool += 4 + sets * 2;
                        break;
                    case 1:
                        player.Wounds = System.Math.Max(0, player.Wounds - (2 + sets));
                        break;
                    default:
                        player.Deck.DrawExact(2 + sets);
                        break;
                }
            }
        }
    }
}
