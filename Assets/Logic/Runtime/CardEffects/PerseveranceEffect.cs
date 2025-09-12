using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 不屈不撓：四擇一的數值提升，棄創傷或多牌獲得額外加成。
    /// option 0移動 1影響力 2攻擊 3格擋
    /// </summary>
    public sealed class PerseveranceEffect : ICardEffect
    {
        private readonly bool _enh;
        public PerseveranceEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int baseVal = _enh ? 3 : 2;
            int bonus = 0;
            if (!_enh)
            {
                if (ctx.CardToDiscard != null && ctx.CardToDiscard.Type == CardType.Wound)
                {
                    player.Deck.Discard(ctx.CardToDiscard);
                    bonus = 1;
                }
                ctx.CardToDiscard = null;
            }
            else
            {
                if (ctx.CardsToDiscard.Count > 0 && ctx.CardsToDiscard.Exists(c => c.Type == CardType.Wound))
                {
                    foreach (var c in ctx.CardsToDiscard)
                        player.Deck.Discard(c);
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
