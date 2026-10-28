using MK.Logic.Core;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 黑暗護符：在白晝使用時降低沙漠移動費並允許黑色法力，可獲得任意顏色魔力標記。
    /// option 低8位起依序指定每顆標記的顏色索引。
    /// </summary>
    public sealed class DarkAmuletEffect : ICardEffect
    {
        private readonly bool _once;
        public DarkAmuletEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int count = _once ? 3 : 1;
            // Validate the complete selection before granting any token or turn permission.
            if (option < 0 || (option >> (8 * count)) != 0)
                throw new System.InvalidOperationException("黑暗护符必须选择正确数量的魔力颜色");
            for (int i = 0; i < count; i++)
                if (((option >> (8 * i)) & 0xFF) > 5)
                    throw new System.InvalidOperationException("黑暗护符的魔力颜色无效");
            for (int i = 0; i < count; i++)
            {
                ManaColor color = (ManaColor)((option >> (8 * i)) & 0xFF);
                player.Mana.AddToken(color, 1);
            }
            if (ctx.DayPart == DayPart.Day)
            {
                ctx.TerrainCostOverride[TerrainType.Desert] = System.Math.Min(3, ctx.TerrainCostOverride.GetValueOrDefault(TerrainType.Desert, 3));
                ctx.AllowBlackAtDay = true;
            }
        }
    }
}
