using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 不屈不撓：四擇一的數值提升，棄創傷或多牌獲得額外加成。
    /// option 0移動 1影響力 2攻擊 3格擋
    /// </summary>
    public sealed class PerseveranceEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public PerseveranceEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 3) throw new InvalidOperationException("不屈不挠必须选择移动、影响力、攻击或格挡");
            var materials = _enh ? ctx.CardsToDiscard.ToArray()
                : ctx.CardToDiscard == null ? Array.Empty<DeedCard>() : new[] { ctx.CardToDiscard };
            if (materials.Count(c => c?.Type == CardType.Wound) > 1
                || (!_enh && materials.Any(c => c?.Type != CardType.Wound)))
                throw new InvalidOperationException("基础只能弃一张伤牌，强化材料至多含一张伤牌");
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, materials[i]))
                    || materials.Take(i).Any(c => ReferenceEquals(c, materials[i])))
                    throw new InvalidOperationException("弃牌材料必须是不同的实际自有手牌");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            int baseVal = _enh ? 3 : 2;
            int bonus = 0;
            if (!_enh)
            {
                if (ctx.CardToDiscard != null && ctx.CardToDiscard.Type == CardType.Wound)
                {
                    player.Deck.TryDiscardExact(ctx.CardToDiscard);
                    bonus = 1;
                }
                ctx.CardToDiscard = null;
            }
            else
            {
                if (ctx.CardsToDiscard.Count > 0)
                {
                    foreach (var c in ctx.CardsToDiscard)
                        player.Deck.TryDiscardExact(c);
                    bonus = ctx.CardsToDiscard.Count * 2;
                }
                ctx.CardsToDiscard.Clear();
            }
            int val = baseVal + bonus;
            switch (option)
            {
                case 0:
                    ctx.MovementPool += val;
                    break;
                case 1:
                    ctx.InfluencePool += val;
                    break;
                case 2:
                    ctx.MeleePool += val;
                    break;
                default:
                    ctx.BlockPool += val;
                    break;
            }
        }
    }
}
