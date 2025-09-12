using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 地道穿行 / 地道攻擊：提供特殊傳送能力。
    /// </summary>
    public sealed class TunnelTravelEffect : ICardEffect
    {
        private readonly bool _enh;
        public TunnelTravelEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.TeleportRange = 3;
            ctx.TeleportForbidden.Add(TerrainType.Swamp);
            ctx.TeleportForbidden.Add(TerrainType.Lake);
            if (!_enh)
            {
                ctx.TeleportMustEndSafe = true;
                ctx.IgnoreRampaging = true;
            }
            else
            {
                ctx.TeleportMustEndFort = true;
                ctx.IgnoreFortified = true;
                ctx.ReturnAfterRetreat = true;
            }
        }
    }
}
