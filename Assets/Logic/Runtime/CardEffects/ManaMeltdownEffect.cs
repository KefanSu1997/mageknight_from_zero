using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力熔毀 / 魔力輻射：操縱其他玩家的魔晶並造成創傷。
    /// option 表示選擇的顏色索引（僅強效）。
    /// </summary>
    public sealed class ManaMeltdownEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaMeltdownEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (ctx.ManaMeltdownOwner != null)
                throw new InvalidOperationException("请先完成魔力熔毁的魔晶选择");
            if (_enh && (option < (int)ManaColor.Red || option > (int)ManaColor.White))
                throw new InvalidOperationException("魔力辐射必须选择红蓝绿白基本色");
            if (!_enh && option != 0)
                throw new InvalidOperationException("魔力熔毁先随机丢弃，再选择所得魔晶");
        }

        /// <summary>随机丢弃完成后选择一个实际丢失的颜色；null 表示放弃获取。</summary>
        public static void ChooseCrystal(PlayerState player, ActionContext ctx, ManaColor? color)
        {
            if (!ReferenceEquals(ctx.ManaMeltdownOwner, player))
                throw new InvalidOperationException("没有属于该玩家的魔力熔毁选择");
            if (color.HasValue && !ctx.ManaMeltdownChoices.Contains(color.Value))
                throw new InvalidOperationException("只能取得本次实际丢弃的魔晶颜色");
            if (color.HasValue) player.Mana.AddCrystal(color.Value, 1);
            ctx.ManaMeltdownChoices.Clear();
            ctx.ManaMeltdownOwner = null;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                // 先按结算前库存判定无晶玩家，不能在丢掉最后一颗后误加创伤。
                foreach (var participant in ctx.OtherPlayers.Append(player))
                    if (!participant.Mana.Crystals.Values.Any(n => n > 0))
                        participant.Wounds += 1;

                var lostColors = new List<ManaColor>();
                var random = ctx.EffectRandom ?? new Random();
                foreach (var op in ctx.OtherPlayers)
                {
                    var list = op.Mana.Crystals.Where(p => p.Value > 0)
                        .SelectMany(p => Enumerable.Repeat(p.Key, p.Value)).ToList();
                    if (list.Count == 0) continue;
                    var color = list[random.Next(list.Count)];
                    op.Mana.Crystals[color] -= 1;
                    if (op.Mana.Crystals[color] == 0) op.Mana.Crystals.Remove(color);
                    if (!lostColors.Contains(color)) lostColors.Add(color);
                }
                if (lostColors.Count == 1)
                    player.Mana.AddCrystal(lostColors[0], 1);
                else if (lostColors.Count > 1)
                {
                    ctx.ManaMeltdownChoices.AddRange(lostColors);
                    ctx.ManaMeltdownOwner = player;
                }
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
