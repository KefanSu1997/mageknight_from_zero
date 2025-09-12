using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 冰霜之橋：降低沼澤與湖泊移動費用。
    /// </summary>
    public sealed class FrostBridgeEffect : ICardEffect
    {
        private readonly bool _enh;
        public FrostBridgeEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 2;
                ctx.TerrainCostOverride[TerrainType.Swamp] = 1;
            }
            else
            {
                ctx.MovementPool += 4;
                ctx.TerrainCostOverride[TerrainType.Swamp] = 1;
                ctx.TerrainCostOverride[TerrainType.Lake] = 1;
            }
        }
    }
}

