using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 求援：依手牌與部隊傷牌數量額外獲得影響力。
    /// </summary>
    public sealed class CallForHelpEffect : ICardEffect
    {
        private readonly bool _enh;
        public CallForHelpEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int baseVal = _enh ? 5 : 3;
            int per = _enh ? 2 : 1;
            int wounds = player.Deck.Hand.Count(c => c.Type == CardType.Wound);
            wounds += player.Units.Sum(u => u.Wounds);
            ctx.InfluencePool += baseVal + wounds * per;
        }
    }
}
