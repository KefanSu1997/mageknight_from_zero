using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力熔毀 / 魔力輻射：操縱其他玩家的魔晶並造成創傷。
    /// option 表示選擇的顏色索引（僅強效）。
    /// </summary>
    public sealed class ManaMeltdownEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaMeltdownEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ManaColor? gain = null;
                foreach (var op in ctx.OtherPlayers)
                {
                    if (op.Mana.Crystals.Count == 0)
                    {
                        op.Wounds += 1;
                        continue;
                    }
                    var list = op.Mana.Crystals.Keys.ToList();
#if NETSTANDARD2_1
                    var color = list[new Random().Next(list.Count)];
#else
                    var color = list[Random.Shared.Next(list.Count)];
#endif
                    op.Mana.Crystals[color] -= 1;
                    if (op.Mana.Crystals[color] == 0) op.Mana.Crystals.Remove(color);
                    gain ??= color;
                }
                if (gain.HasValue)
                    player.Mana.AddCrystal(gain.Value, 1);
            }
            else
            {
                ManaColor color = (ManaColor)option;
                foreach (var p in ctx.OtherPlayers.Append(player))
                {
                    if (p.Mana.Crystals.TryGetValue(color, out int n) && n > 0)
                        p.Wounds += n;
                }
                player.Mana.AddCrystal(color, 2);
            }
        }
    }
}
