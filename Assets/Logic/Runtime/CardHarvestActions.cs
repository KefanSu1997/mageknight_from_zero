using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>野蛮收获的材料验证与单格移动成功后的结算，由正式移动命令调用。</summary>
    internal static class CardHarvestActions
    {
        internal static ManaColor Validate(PlayerState player, ActionContext context,
            DeedCard material, ManaColor? chosen)
        {
            if (context.HarvestTriggers == 0)
                throw new InvalidOperationException("本回合没有剩余的野蛮收获次数");
            if (material == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, material)))
                throw new InvalidOperationException("野蛮收获必须选择实际持有的手牌");
            var printed = CardColorHelper.Get(material.Id);
            var color = material.Type == CardType.Artifact ? chosen : printed;
            if (!color.HasValue || color.Value < ManaColor.Red || color.Value > ManaColor.White
                || material.Type != CardType.Artifact && chosen.HasValue && chosen != printed)
                throw new InvalidOperationException("收获必须选择同色魔晶；神器可选择一种基本色");
            return color.Value;
        }

        internal static void Resolve(PlayerState player, ActionContext context,
            DeedCard material, ManaColor color)
        {
            if (!player.Deck.TryDiscardExact(material))
                throw new InvalidOperationException("收获结算时丢失真实手牌材料");
            player.Mana.AddCrystal(color);
            if (context.HarvestTriggers > 0) context.HarvestTriggers--;
        }
    }
}
