using MK.Logic.Core;

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
            for (int i = 0; i < count; i++)
            {
                ManaColor color = (ManaColor)System.Math.Clamp((option >> (8 * i)) & 0xFF, 0, 5);
                player.Mana.AddToken(color, 1);
            }
            if (ctx.DayPart == DayPart.Day)
            {
                ctx.TerrainCostOverride[TerrainType.Desert] = 3;
                ctx.AllowBlackAtDay = true;
            }
        }
    }
}
