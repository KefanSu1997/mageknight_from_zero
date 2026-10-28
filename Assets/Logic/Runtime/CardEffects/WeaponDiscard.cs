using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    internal static class WeaponDiscard
    {
        public static void Validate(PlayerState player, ActionContext context)
        {
            for (int i = 0; i < context.CardsToDiscard.Count; i++)
            {
                var card = context.CardsToDiscard[i];
                if (card == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, card)))
                    throw new InvalidOperationException("武器必须弃掉实际持有的手牌实例");
                if (context.CardsToDiscard.Take(i).Any(c => ReferenceEquals(c, card)))
                    throw new InvalidOperationException("武器不能重复选择同一张手牌");
                if (card.Type != CardType.Action && card.Type != CardType.Spell && card.Type != CardType.Artifact)
                    throw new InvalidOperationException("武器不能弃掉创伤或非契约卡牌");
            }
        }

        public static int Discard(PlayerState player, ActionContext context)
        {
            Validate(player, context);
            int count = context.CardsToDiscard.Count;
            foreach (var card in context.CardsToDiscard) player.Deck.TryDiscardExact(card);
            context.CardsToDiscard.Clear();
            return count;
        }
    }
}
