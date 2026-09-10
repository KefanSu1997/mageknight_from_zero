using MK.Logic.Core;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 德魯伊之道：移動並降低移動成本。
    /// </summary>
    public sealed class DruidicPathsEffect : ICardEffect, ICardEffectValidator
    {
        private readonly int _move;
        private readonly bool _terrain;
        public DruidicPathsEffect(int move, bool byTerrain)
        {
            _move = move;
            _terrain = byTerrain;
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0) throw new System.InvalidOperationException("德鲁伊之道的效果选项无效");
            if (_terrain ? !ctx.TargetTerrain.HasValue || !System.Enum.IsDefined(typeof(TerrainType), ctx.TargetTerrain.Value)
                : !ctx.TargetHex.HasValue || ctx.MovementMap == null || !ctx.MovementMap.Placed.ContainsKey(ctx.TargetHex.Value))
                throw new System.InvalidOperationException("德鲁伊之道必须选择有效六角格或地形");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.MovementPool += _move;
            if (_terrain)
                ctx.TerrainMoveReduction[ctx.TargetTerrain.Value] = ctx.TerrainMoveReduction.GetValueOrDefault(ctx.TargetTerrain.Value) + 1;
            else
                ctx.HexMoveReduction[ctx.TargetHex.Value] = ctx.HexMoveReduction.GetValueOrDefault(ctx.TargetHex.Value) + 1;
        }
    }
}
