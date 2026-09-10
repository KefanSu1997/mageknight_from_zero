using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 德魯伊法杖：棄牌選擇不同效果，強效可執行兩次且免棄牌。
    /// option 低8位為第1項目，次8位為其參數；高位相同結構表示第2項目。
    /// 0=白色傳送、1=藍色得晶體、2=紅色重整部隊、3=綠色治療。
    /// </summary>
    public sealed class DruidStaffEffect : ICardEffect
    {
        private readonly bool _once;
        public DruidStaffEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                if (ctx.CardToDiscard == null)
                    throw new System.InvalidOperationException("未指定棄牌");
                player.Deck.Discard(ctx.CardToDiscard);
                ctx.CardToDiscard = null;
                Apply(option & 0xFF, (option >> 8) & 0xFF, player, ctx);
            }
            else
            {
                Apply(option & 0xFF, (option >> 8) & 0xFF, player, ctx);
                Apply((option >> 16) & 0xFF, (option >> 24) & 0xFF, player, ctx);
            }
        }

        private static void Apply(int opt, int param, PlayerState p, ActionContext ctx)
        {
            switch (opt)
            {
                case 0: // 白色：短距離安全傳送
                    ctx.TeleportRange = 2;
                    ctx.TeleportMustEndSafe = true;
                    break;
                case 1: // 藍色：任意色晶體兩顆
                    var col = (ManaColor)System.Math.Clamp(param, 0, 3);
                    p.Mana.AddCrystal(col, 2);
                    break;
                case 2: // 紅色：重整3級以下部隊
                    var unit = ctx.TargetUnit;
                    if (unit != null && unit.Card.Level <= 3)
                        unit.Ready();
                    break;
                case 3: // 綠色：治療3
                    CardHealing.Heal(p, ctx, 3);
                    break;
            }
        }
    }
}
