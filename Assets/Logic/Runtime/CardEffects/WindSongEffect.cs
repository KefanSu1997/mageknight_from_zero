using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 清風之歌：降低平原、沙漠與荒原移動費用，可跨湖泊。
    /// </summary>
    public sealed class WindSongEffect : ICardEffect
    {
        private readonly bool _enh;
        public WindSongEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += 2;
            int reduce = _enh ? 2 : 1;
            _apply(ctx, TerrainType.Plains, reduce);
            _apply(ctx, TerrainType.Desert, reduce);
            _apply(ctx, TerrainType.Wasteland, reduce);
            if (_enh)
            {
                ctx.TerrainCostOverride[TerrainType.Lake] = 0;
                ctx.RequireBlueForLake = true;
            }
        }

        private static void _apply(ActionContext ctx, TerrainType t, int reduce)
        {
            int baseCost = Map.TerrainCost.GetCost(t, ctx.DayPart);
            ctx.TerrainCostOverride[t] = System.Math.Max(0, baseCost - reduce);
        }
    }
}

