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
            ctx.MountainLoreMode = System.Math.Max(ctx.MountainLoreMode, _enh ? 2 : 1);
            if (!_enh)
            {
                ctx.MovementPool += 3;
            }
            else
            {
                ctx.MovementPool += 5;
                ctx.TerrainCostOverride[TerrainType.Mountain] = 5;
                ctx.MountainSafe = true;
            }
        }

        /// <summary>清理本回合许可之前检查实际驻留格，再交给共用补牌入口消费奖励。</summary>
        public static void ResolveEndTurn(PlayerState player, ActionContext ctx)
        {
            if (ctx == null || ctx.MountainLoreMode == 0) return;
            var terrain = ctx.CurrentTerrain;
            if (ctx.MovementMap != null && ctx.MovementMap.Placed.TryGetValue(player.Position, out var tile)
                && tile.Edges.Length > 0)
                terrain = tile.Edges[0];
            if (terrain == TerrainType.Mountain
                && !CardTravelActions.IsSafeDestination(player, ctx, player.Position, ctx.OtherPlayers))
                throw new System.InvalidOperationException("山川逸闻结束时必须能够安全驻留该山脉格");
            int bonus = terrain == TerrainType.Hills ? 1
                : terrain == TerrainType.Mountain && ctx.MountainLoreMode == 2 ? 2 : 0;
            ctx.NextDrawBonus += bonus;
            ctx.MountainLoreMode = 0;
        }
    }
}
