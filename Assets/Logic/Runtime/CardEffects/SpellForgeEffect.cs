using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 法術熔爐：從法術市場獲取相應顏色的魔晶。
    /// 基础 option 为实际供给索引；强化低8位、高8位各为一张不同法术的供给索引。
    /// </summary>
    public sealed class SpellForgeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public SpellForgeEffect(bool enhanced) => _enh = enhanced;

        private ManaColor[] SelectedColors(ActionContext ctx, int option)
        {
            var indices = _enh ? new[] { option & 255, option >> 8 } : new[] { option };
            if (option < 0 || ctx.SpellSupply == null
                || indices.Any(i => i < 0 || i >= ctx.SpellSupply.Offer.Count))
                throw new InvalidOperationException("法术熔炉必须选择实际供给中的法术");
            if (_enh && (indices[0] == indices[1]
                || ReferenceEquals(ctx.SpellSupply.Offer[indices[0]], ctx.SpellSupply.Offer[indices[1]])))
                throw new InvalidOperationException("法术熔炉强效必须选择两张不同法术");

            var definitions = CardJsonLoader.LoadSpells();
            return indices.Select(i =>
            {
                var definition = definitions.FirstOrDefault(c => c.Id == ctx.SpellSupply.Offer[i].Id);
                if (definition == null)
                    throw new InvalidOperationException("法术熔炉供给缺少原版法术定义");
                return definition.ManaColor;
            }).ToArray();
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
            => SelectedColors(ctx, option);

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            // Two different spells may share a color: each selected spell grants its own crystal.
            foreach (var color in SelectedColors(ctx, option)) player.Mana.AddCrystal(color, 1);
        }
    }
}
