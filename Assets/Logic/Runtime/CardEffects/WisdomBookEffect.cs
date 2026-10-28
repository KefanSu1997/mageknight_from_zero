using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>永久去除实际手牌中的行动牌，领取玩家选中的同色供给到手牌。</summary>
    public sealed class WisdomBookEffect : ICardEffect
    {
        private readonly bool _once;
        public WisdomBookEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var material = ctx.CardToRemove;
            if (material == null || material.Type != CardType.Action
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, material)))
                throw new InvalidOperationException("智慧之书必须去除实际持有的一张行动牌");
            var actions = CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions());
            var original = actions.FirstOrDefault(c => c.Id == material.Id);
            var color = original == null ? null : CardColorHelper.Get(original.Id);
            if (!color.HasValue || color.Value < ManaColor.Red || color.Value > ManaColor.White)
                throw new InvalidOperationException("智慧之书材料必须具有原版行动牌的颜色");

            // Validate the full offered-card selection before permanently removing the material.
            string acquiredId;
            if (!_once)
            {
                var supply = ctx.AdvActionSupply;
                if (supply == null || option < 0 || option >= supply.Offer.Count)
                    throw new InvalidOperationException("必须选择实际高级行动供给中的卡牌");
                acquiredId = supply.Offer[option].Id;
                if (!CardJsonLoader.LoadAdvancedActions().Any(c => c.Id == acquiredId)
                    || CardColorHelper.Get(acquiredId) != color.Value)
                    throw new InvalidOperationException("智慧之书只能取得与材料同色的高级行动牌");
            }
            else
            {
                var supply = ctx.SpellSupply;
                if (supply == null || option < 0 || option >= supply.Offer.Count)
                    throw new InvalidOperationException("必须选择实际法术供给中的卡牌");
                acquiredId = supply.Offer[option].Id;
                if (!CardJsonLoader.LoadSpells().Any(c => c.Id == acquiredId)
                    || CardColorHelper.Get(acquiredId) != color.Value)
                    throw new InvalidOperationException("智慧之书只能取得与材料同色的法术牌");
            }

            if (!player.Deck.TryRemovePermanentlyExact(material))
                throw new InvalidOperationException("智慧之书去除材料时丢失实际手牌实例");
            if (_once) ctx.SpellSupply.Claim(option); else ctx.AdvActionSupply.Take(option);
            player.Deck.Hand.Add(new DeedCard(acquiredId, _once ? CardType.Spell : CardType.Action));
            if (_once) player.Mana.AddCrystal(color.Value);
            ctx.CardToRemove = null;
        }
    }
}
