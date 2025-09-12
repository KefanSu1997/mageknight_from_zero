using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 隨機應變：需棄掉另一張手牌後，從四種數值中擇一獲得。
    /// </summary>
    public sealed class ImprovisationEffect : ICardEffect
    {
        private readonly bool _enh;
        public ImprovisationEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            // 必須指定欲棄掉的手牌
            if (ctx.CardToDiscard == null)
                throw new System.InvalidOperationException("未指定要棄掉的卡牌");
            player.Deck.Discard(ctx.CardToDiscard);
            ctx.CardToDiscard = null;

            int val = _enh ? 5 : 3;
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
