using System;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 法術寶典：棄牌施放市場法術，強效可使用強力效果。
    /// option 為目標法術在供應中的索引。
    /// </summary>
    public sealed class SpellTomeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public SpellTomeEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var payment = ctx.CardToDiscard;
            if (payment == null || !player.Deck.Hand.Any(c => ReferenceEquals(c,payment)))
                throw new InvalidOperationException("法术宝典必须弃掉实际持有的有色手牌实例");
            if ((payment.Type != CardType.Action && payment.Type != CardType.Spell) || !CardColorHelper.Get(payment.Id).HasValue)
                throw new InvalidOperationException("法术宝典弃牌必须具有原版颜色");
            if (ctx.SpellSupply == null || option < 0 || option >= ctx.SpellSupply.Offer.Count)
                throw new InvalidOperationException("法术宝典必须选择当前供给中的法术");
            var spell = CardJsonLoader.LoadSpells().SingleOrDefault(s=>s.Id==ctx.SpellSupply.Offer[option].Id);
            if (spell == null || spell.ManaColor != CardColorHelper.Get(payment.Id))
                throw new InvalidOperationException("法术宝典供给法术必须与弃牌同色");
            if (spell.Id == "magic_011" && ctx.CardsToDiscard.Any(c=>ReferenceEquals(c,payment)))
                throw new InvalidOperationException("宝典的弃牌费用不能再次用于献祭");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player,ctx,option);
            var payment=ctx.CardToDiscard;
            var spell=CardJsonLoader.LoadSpells().Single(s=>s.Id==ctx.SpellSupply.Offer[option].Id);
            var data=new ActionCardData(spell.Id,spell.Name,spell.Set,spell.ImagePath,spell.EnImagePath,
                spell.Top.Effect,spell.Bottom.Effect,spell.Top.ManaCost);
            new ActionSystem().PlayFromTome(data,player,ctx,_once,ctx.TomeSpellOption,()=>
            {
                player.Deck.TryDiscardExact(payment);
                ctx.CardToDiscard=null;
            });
        }
    }
}
