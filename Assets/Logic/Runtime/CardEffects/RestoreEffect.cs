using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 復原 / 重生：治療並依地形重整部隊。
    /// </summary>
    public sealed class RestoreEffect : ICardEffect
    {
        private readonly bool _enh;
        public RestoreEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int heal = ctx.CurrentTerrain == TerrainType.Forest ? 5 : 3;
            CardHealing.Heal(player, ctx, heal);
            if (_enh && ctx.TargetUnit != null)
            {
                int limit = ctx.CurrentTerrain == TerrainType.Forest ? 5 : 3;
                if (ctx.TargetUnit.Card.Level <= limit)
                    ctx.TargetUnit.Ready();
            }
        }
    }
}
