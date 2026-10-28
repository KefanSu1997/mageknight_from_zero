using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 思維讀取 / 思維竊取：迫使其他玩家棄牌並可獲得其中一張。
    /// option 為顏色索引。
    /// </summary>
    public sealed class MindReadEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public MindReadEffect(bool enhanced) => _enh = enhanced;

        private static bool Matches(DeedCard card, ManaColor color)
            => (card.Type == CardType.Action || card.Type == CardType.Spell)
                && CardColorHelper.Get(card.Id) == color;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < (int)ManaColor.Red || option > (int)ManaColor.White)
                throw new InvalidOperationException("思维读取必须选择四种基本色之一");
            if (ctx.MindReadChoices.Count != ctx.OtherPlayers.Count)
                throw new InvalidOperationException("必须等待每位其他玩家选择弃牌或展示手牌");
            for (int i = 0; i < ctx.OtherPlayers.Count; i++)
            {
                var other = ctx.OtherPlayers[i];
                if (other == null || ReferenceEquals(other, player)
                    || ctx.OtherPlayers.Take(i).Any(p => ReferenceEquals(p, other)))
                    throw new InvalidOperationException("其他玩家列表必须是不同的实际玩家");
                if (!ctx.MindReadChoices.TryGetValue(other, out var selected))
                    throw new InvalidOperationException("缺少该玩家的实际选择");
                var matching = other.Deck.Hand.Where(c => Matches(c, (ManaColor)option));
                if (selected == null ? matching.Any() : !matching.Any(c => ReferenceEquals(c, selected)))
                    throw new InvalidOperationException("须弃掉所选同色行动/法术；只有没有同色牌时才能展示手牌");
            }
            if (ctx.MindStealTarget != null && (!_enh || ctx.MindStealTarget.Type != CardType.Action
                || !ctx.MindReadChoices.Values.Any(c => ReferenceEquals(c, ctx.MindStealTarget))))
                throw new InvalidOperationException("只能选择本次其他玩家弃掉的一张行动牌，或放弃偷取");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            player.Mana.AddCrystal((ManaColor)option, 1);
            ctx.MindReadRevealedHands.Clear();
            foreach (var other in ctx.OtherPlayers)
            {
                var selected = ctx.MindReadChoices[other];
                if (selected == null) ctx.MindReadRevealedHands[other] = other.Deck.Hand.ToArray();
                else
                {
                    if (!other.Deck.TryDiscardExact(selected))
                        throw new InvalidOperationException("所选手牌实例在结算前已离开手牌");
                    if (ReferenceEquals(selected, ctx.MindStealTarget))
                    {
                        if (!other.Deck.TryRemoveFromDiscardExact(selected))
                            throw new InvalidOperationException("本次实际弃下的行动牌不在原主弃牌区");
                        player.Deck.Hand.Add(selected);
                    }
                }
            }
            ctx.MindReadChoices.Clear();
            ctx.MindStealTarget = null;
        }
    }
}
