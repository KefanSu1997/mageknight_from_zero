using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 星辰之弓：棄牌換取遠程攻擊，擊殺獲名望；強效令遠程及攻城攻擊翻倍或互換。
    /// </summary>
    public sealed class StarBowEffect : ICardEffect
    {
        private readonly bool _once;
        public StarBowEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                int cnt = 0;
                foreach (var c in ctx.CardsToDiscard)
                {
                    if (player.Deck.RemoveFromHand(c))
                        cnt++;
                }
                ctx.CardsToDiscard.Clear();
                ctx.RangedPool += cnt * 2;
                ctx.PendingFameOnKill += 1;
            }
            else
            {
                ctx.DoubleRangedAttack = true;
                ctx.RangedAttackAsSiege = true;
                ctx.DoubleSiegeAttack = true;
                ctx.SiegeAttackAsRanged = true;
            }
        }
    }
}
