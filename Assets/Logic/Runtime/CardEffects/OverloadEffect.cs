using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 效能過載：移除一張牌並重複其效果。
    /// option 為要觸發的效果枚舉值。
    /// </summary>
    public sealed class OverloadEffect : ICardEffect
    {
        private readonly bool _enh;
        public OverloadEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToRemove == null)
                throw new System.InvalidOperationException("未指定要去除的卡牌");
            player.Deck.RemoveFromHand(ctx.CardToRemove);
            ctx.CardToRemove = null;

            var id = (ActionEffectId)option;
            var eff = CardEffectFactory.Get(id);
            int times = _enh ? 2 : 3;
            for (int i = 0; i < times; i++)
                eff.Execute(player, ctx);
        }
    }
}
