using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 远古血脉：基础支付同色魔力取得供给；强化借用原卡强效，牌留原处。
    /// 基础option低8位为供给索引、高位为支付颜色；强化option为供给索引。
    /// 强效内部选项和伤牌去向分别使用BloodlineEffectOption及BloodlineWoundToDiscard。
    /// </summary>
    public sealed class AncientBloodlineEffect : ICardEffect, ICardEffectValidator, ICardContextualManaCost
    {
        private readonly bool _enh;
        public AncientBloodlineEffect(bool enhanced) => _enh = enhanced;

        private ActionCardData Selected(ActionContext context, int option)
        {
            int index = _enh ? option : option & 255;
            var supply = context.AdvActionSupply;
            if (option < 0 || supply == null || index < 0 || index >= supply.Offer.Count)
                throw new InvalidOperationException("远古血脉必须选择实际高级行动供给");
            var card = CardJsonLoader.LoadAdvancedActions().FirstOrDefault(c => c.Id == supply.Offer[index].Id);
            if (card == null || string.IsNullOrWhiteSpace(card.Name) || string.IsNullOrWhiteSpace(card.BaseEffect)
                || string.IsNullOrWhiteSpace(card.EnhancedEffect))
                throw new InvalidOperationException("远古血脉必须选择有效原版高级行动牌");
            return card;
        }

        private T WithBorrowedEffect<T>(ActionContext context, int option, Func<ICardEffect,T> action)
        {
            if (!context.BloodlineBorrowing.Add(option))
                throw new InvalidOperationException("远古血脉的供给选择形成循环，必须选择可结算的效果");
            try { return action(new ActionSystem().ResolveAdvancedStrongEffect(Selected(context, option))); }
            finally { context.BloodlineBorrowing.Remove(option); }
        }

        public void Validate(PlayerState player, ActionContext context, int option)
        {
            var card = Selected(context, option);
            if (!_enh)
            {
                var color = (ManaColor)(option >> 8);
                if (color < ManaColor.Red || color > ManaColor.White || CardColorHelper.Get(card.Id) != color)
                    throw new InvalidOperationException("远古血脉必须支付与所获高级行动同色的魔力");
                if (context.BloodlineWoundToDiscard)
                    throw new InvalidOperationException("远古血脉基础效果的伤牌必须加入手牌");
                return;
            }
            WithBorrowedEffect(context, option, effect =>
            {
                if (effect is ICardEffectValidator validator)
                    validator.Validate(player, context, context.BloodlineEffectOption);
                return true;
            });
        }

        public ManaColor[] GetAdditionalManaCost(PlayerState player, ActionContext context, int option)
        {
            if (!_enh) return new[] { (ManaColor)(option >> 8) };
            return WithBorrowedEffect(context, option, effect =>
            {
                var cost = effect is ICardAdditionalManaCost extra
                    ? extra.GetAdditionalManaCost(context.BloodlineEffectOption) : Array.Empty<ManaColor>();
                if (effect is ICardContextualManaCost nested)
                    cost = cost.Concat(nested.GetAdditionalManaCost(player, context, context.BloodlineEffectOption)).ToArray();
                return cost;
            });
        }

        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            player.AddWoundCards(1, _enh && context.BloodlineWoundToDiscard);
            if (!_enh)
            {
                var card = context.AdvActionSupply.Take(option & 255);
                player.Deck.Hand.Add(new DeedCard(card.Id, CardType.Action));
                return;
            }
            // The enclosing ActionSystem applies post-effect modifiers exactly once.
            // Extra selected costs were paid together with the host's printed cost.
            WithBorrowedEffect(context, option, effect =>
            {
                effect.Execute(player, context, context.BloodlineEffectOption);
                return true;
            });
        }
    }
}
