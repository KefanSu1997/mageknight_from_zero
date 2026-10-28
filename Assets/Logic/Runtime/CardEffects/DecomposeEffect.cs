using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 分解：移除一張手牌並依顏色獲取魔晶。
    /// 颜色只能来自实际移除的原行动牌，option不能伪造材料颜色。
    /// </summary>
    public sealed class DecomposeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public DecomposeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var material = ctx.CardToRemove;
            if (material == null || material.Type != CardType.Action
                || !player.Deck.Hand.Any(card => ReferenceEquals(card, material)))
                throw new InvalidOperationException("分解必须选择实际手牌中的行动牌");
            var original = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions())
                .FirstOrDefault(card => card.Id == material.Id);
            if (original == null || string.IsNullOrWhiteSpace(original.Name) || string.IsNullOrWhiteSpace(original.BaseEffect)
                || !CardColorHelper.Get(material.Id).HasValue)
                throw new InvalidOperationException("分解材料必须是具有原版颜色的有效行动牌");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var color = CardColorHelper.Get(ctx.CardToRemove.Id).Value;
            if (!player.Deck.TryRemovePermanentlyExact(ctx.CardToRemove))
                throw new InvalidOperationException("分解去除材料时丢失实际手牌实例");
            ctx.CardToRemove = null;
            if (!_enh)
            {
                player.Mana.AddCrystal(color, 2);
            }
            else
            {
                foreach (ManaColor c in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                {
                    if (c == color) continue;
                    player.Mana.AddCrystal(c, 1);
                }
            }
        }
    }
}
