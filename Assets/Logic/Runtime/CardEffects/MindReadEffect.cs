using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 思維讀取 / 思維竊取：迫使其他玩家棄牌並可獲得其中一張。
    /// option 為顏色索引。
    /// </summary>
    public sealed class MindReadEffect : ICardEffect
    {
        private readonly bool _enh;
        public MindReadEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            player.Mana.AddCrystal(color,1);
            DeedCard? steal = null;
            foreach (var op in ctx.OtherPlayers)
            {
                if (op.Deck.Hand.Count == 0) continue;
                var card = op.Deck.Hand[0];
                op.Deck.Hand.RemoveAt(0);
                op.Deck.Discard(card);
                if (_enh && steal == null && card.Type == CardType.Action)
                    steal = card;
            }
            if (steal != null)
                player.Deck.Hand.Add(steal);
        }
    }
}
