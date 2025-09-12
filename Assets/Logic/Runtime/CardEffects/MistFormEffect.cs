using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 迷霧形態 / 迷霧帷幕：調整移動與抗性效果。
    /// </summary>
    public sealed class MistFormEffect : ICardEffect
    {
        private readonly bool _enh;
        public MistFormEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 4;
#if NETSTANDARD2_1
                foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
                    ctx.TerrainCostOverride[(TerrainType)t] = 2;
#else
                foreach (TerrainType t in System.Enum.GetValues<TerrainType>())
                    ctx.TerrainCostOverride[t] = 2;
#endif
                ctx.MoveForbidden.Add(TerrainType.Hills);
                ctx.MoveForbidden.Add(TerrainType.Mountain);
            }
            else
            {
                ctx.UnitsResistAll = true;
                ctx.FirstWoundIgnored = true;
            }
        }
    }
}
