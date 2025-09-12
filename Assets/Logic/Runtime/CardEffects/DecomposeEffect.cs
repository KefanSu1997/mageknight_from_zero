using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 分解：移除一張手牌並依顏色獲取魔晶。
    /// option 參數指定被移除卡牌的顏色索引。
    /// </summary>
    public sealed class DecomposeEffect : ICardEffect
    {
        private readonly bool _enh;
        public DecomposeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToRemove == null)
                throw new System.InvalidOperationException("未指定要去除的卡牌");
            player.Deck.RemoveFromHand(ctx.CardToRemove);
            ctx.CardToRemove = null;

            var color = (ManaColor)System.Math.Clamp(option, 0, 3);
            if (!_enh)
            {
                player.Mana.AddCrystal(color, 2);
            }
            else
            {
                foreach (ManaColor c in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                {
                    if (c == color) continue;
                    player.Mana.AddCrystal(c, 1);
                }
            }
        }
    }
}
