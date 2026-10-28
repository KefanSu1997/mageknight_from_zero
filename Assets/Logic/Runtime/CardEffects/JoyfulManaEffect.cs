using MK.Logic.Core;
using System.Collections.Generic;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 歡悅魔晶：消耗或獲得魔力晶體，回合結束可棄另一張牌收回本牌。
    /// </summary>
    public sealed class JoyfulManaEffect : ICardEffect
    {
        private readonly bool _enh;
        public JoyfulManaEffect(bool enhanced) => _enh = enhanced;


        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option < 0 || option > 3) throw new System.ArgumentOutOfRangeException(nameof(option));
            ManaColor color = (ManaColor)option;
            if (!_enh)
            {
                if (!player.Mana.TryPayExactColors(new[] { color }, ctx, player))
                    throw new System.InvalidOperationException("法力不足，不能晶化");
                player.Mana.AddCrystal(color,1);
            }
            else
            {
                player.Mana.AddCrystal(color,1);
            }
        }

        internal static void SelectReturn(PlayerState player, ActionContext ctx, DeedCard card, DeedCard payment)
        {
            ValidateReturn(player, ctx, card, payment);
            ctx.JoyfulManaReturnCard = card;
            ctx.JoyfulManaReturnPayment = payment;
        }

        private static void ValidateReturn(PlayerState player, ActionContext ctx, DeedCard card, DeedCard payment)
        {
            var permission = ctx.JoyfulManaPlayed.FirstOrDefault(entry => ReferenceEquals(entry.Card, card));
            if (permission.Card == null || !player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card)))
                throw new InvalidOperationException("必须选择本回合实际打出的欢悦魔晶");
            if (payment == null || ReferenceEquals(card, payment)
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, payment)))
                throw new InvalidOperationException("收回欢悦魔晶必须弃掉另一张实际手牌");
            if (!permission.Enhanced && payment.Type == CardType.Wound)
                throw new InvalidOperationException("基础欢悦魔晶不能用创伤卡支付收回费用");
        }

        internal static void ResolveReturn(PlayerState player, ActionContext? ctx)
        {
            if (ctx == null) return;
            if (ctx.JoyfulManaReturnCard != null)
            {
                var card = ctx.JoyfulManaReturnCard;
                var payment = ctx.JoyfulManaReturnPayment;
                // Validate again before any end-turn mutation: a selected payment may have been played meanwhile.
                ValidateReturn(player, ctx, card, payment);
                player.Deck.TryDiscardExact(payment);
                player.Deck.TryTakePlayedExact(card);
                player.Deck.Hand.Add(card);
            }
            ctx.JoyfulManaPlayed.Clear();
            ctx.JoyfulManaReturnCard = ctx.JoyfulManaReturnPayment = null;
        }
    }
}
