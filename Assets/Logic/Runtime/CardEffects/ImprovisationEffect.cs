using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 隨機應變：需棄掉另一張手牌後，從四種數值中擇一獲得。
    /// </summary>
    public sealed class ImprovisationEffect : ICardEffect, ICardEffectValidator, IFixedCardOutput
    {
        private readonly bool _enh;
        public ImprovisationEffect(bool enhanced) => _enh = enhanced;

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] {
            new FixedCardOutput(0, FixedOutputKind.Movement, _enh ? 5 : 3),
            new FixedCardOutput(2, FixedOutputKind.Attack, _enh ? 5 : 3),
            new FixedCardOutput(3, FixedOutputKind.Block, _enh ? 5 : 3) };
        public void Validate(PlayerState player,ActionContext ctx,int option)
        {
            if(option<0 || option>3 || ctx.CardToDiscard==null || ctx.CardToDiscard.Type==CardType.Wound
                || !player.Deck.Hand.Any(c=>ReferenceEquals(c,ctx.CardToDiscard)))
                throw new InvalidOperationException("随机应变须先选择另一张实际非创伤手牌及原四种效果之一");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player,ctx,option);
            player.Deck.Discard(ctx.CardToDiscard);
            ctx.CardToDiscard = null;

            if (option == 1) ctx.InfluencePool += _enh ? 5 : 3;
            else FixedCardOutputs.Add(ctx, FixedOutputs.Single(o => o.Option == option));
        }
    }
}
