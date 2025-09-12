using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 公正之劍：棄牌換取攻擊並在擊殺敵人時獲得名望；強效使物理攻擊翻倍並忽略抗性。
    /// </summary>
    public sealed class JusticeSwordEffect : ICardEffect
    {
        private readonly bool _once;
        public JusticeSwordEffect(bool once) => _once = once;

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
                ctx.MeleePool += cnt * 3;
                ctx.PendingFameOnKill += 1;
            }
            else
            {
                ctx.DoublePhysicalAttack = true;
                ctx.IgnoreResist = true;
                ctx.PendingFameOnKill += 1;
            }
        }
    }
}
