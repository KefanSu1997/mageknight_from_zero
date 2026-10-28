using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔法天賦：棄一牌後可施放同色法術，強效則獲取法術牌。
    /// 基础颜色来自实际弃牌；强化option低8位为额外魔力颜色，高位为供给索引。
    /// </summary>
    public sealed class MagicTalentEffect : ICardEffect, ICardEffectValidator, ICardAdditionalManaCost
    {
        private readonly bool _enh;
        public MagicTalentEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_enh)
            {
                var card = ctx.CardToDiscard;
                if (card == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, card)))
                    throw new InvalidOperationException("魔法天赋必须弃掉实际持有的有色卡牌");
                if ((card.Type != CardType.Action && card.Type != CardType.Spell) || !CardColorHelper.Get(card.Id).HasValue)
                    throw new InvalidOperationException("魔法天赋弃牌必须具有原版颜色");
            }
            else
            {
                int index = option >> 8;
                var color = (ManaColor)(option & 255);
                if (option < 0 || color < ManaColor.Red || color > ManaColor.White || ctx.SpellSupply == null
                    || index >= ctx.SpellSupply.Offer.Count)
                    throw new InvalidOperationException("魔法天赋必须选择当前供给中的法术和有效颜色");
                if (CardColorHelper.Get(ctx.SpellSupply.Offer[index].Id) != color)
                    throw new InvalidOperationException("魔法天赋额外魔力必须与所获法术同色");
            }
        }

        public ManaColor[] GetAdditionalManaCost(int option)
            => _enh ? new[] { (ManaColor)(option & 255) } : Array.Empty<ManaColor>();

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (_enh)
            {
                var spell = ctx.SpellSupply.Claim(option >> 8);
                player.Deck.GainToDiscard(new DeedCard(spell.Id, CardType.Spell));
                return;
            }
            var card = ctx.CardToDiscard;
            var color = CardColorHelper.Get(card.Id).Value;
            player.Deck.TryDiscardExact(card);
            ctx.CardToDiscard = null;
            ctx.SpellColors.Add(color);
        }
    }
}
