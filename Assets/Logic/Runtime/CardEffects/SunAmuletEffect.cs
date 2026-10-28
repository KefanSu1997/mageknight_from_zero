using MK.Logic.Core;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 皓日护符：夜間移動森林減費並可用金色法力。
    /// </summary>
    public sealed class SunAmuletEffect : ICardEffect
    {
        private readonly bool _once;
        public SunAmuletEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int count = _once ? 3 : 1;
            player.Mana.AddToken(ManaColor.Gold, count);
            if (ctx.DayPart == DayPart.Night)
            {
                ctx.TerrainCostOverride[TerrainType.Forest] = System.Math.Min(3, ctx.TerrainCostOverride.GetValueOrDefault(TerrainType.Forest, 3));
                ctx.AllowGoldAtNight = true;
                ctx.RevealSitesAsDay = true;
                Map.SiteVisibility.RevealVisible(player, ctx.MovementMap, ctx.DayPart, ctx);
            }
        }
    }
}
