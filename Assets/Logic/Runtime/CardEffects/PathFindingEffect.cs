using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 迷途尋蹤：降低所有地形移動費用。
    /// </summary>
    public sealed class PathFindingEffect : ICardEffect
    {
        private readonly bool _enh;
        public PathFindingEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 2;
                foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
                {
                    if (t == TerrainType.Lake) continue;
                    int baseCost = Map.TerrainCost.GetCost(t, ctx.DayPart);
                    ctx.TerrainCostOverride[t] = System.Math.Max(2, baseCost - 1);
                }
            }
            else
            {
                ctx.MovementPool += 4;
                foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
                {
                    if (t == TerrainType.Lake) continue;
                    ctx.TerrainCostOverride[t] = 2;
                }
            }
        }
    }
}

