using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 山川逸聞：移動並依停留地形提升下次抽牌上限。
    /// </summary>
    public sealed class MountainLoreEffect : ICardEffect
    {
        private readonly bool _enh;
        public MountainLoreEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MovementPool += 3;
                if (ctx.CurrentTerrain == TerrainType.Hills)
                    ctx.NextDrawBonus = System.Math.Max(ctx.NextDrawBonus, 1);
            }
            else
            {
                ctx.MovementPool += 5;
                ctx.TerrainCostOverride[TerrainType.Mountain] = 5;
                ctx.MountainSafe = true;
                if (ctx.CurrentTerrain == TerrainType.Mountain)
                    ctx.NextDrawBonus = System.Math.Max(ctx.NextDrawBonus, 2);
                else if (ctx.CurrentTerrain == TerrainType.Hills)
                    ctx.NextDrawBonus = System.Math.Max(ctx.NextDrawBonus, 1);
            }
        }
    }
}
