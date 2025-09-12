using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 英雄事跡：招募時追加名望或聲望。
    /// </summary>
    public sealed class DeedsOfGloryEffect : ICardEffect
    {
        private readonly bool _enh;
        public DeedsOfGloryEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += _enh ? 6 : 3;
            ctx.ReputationPerRecruit += 1;
            if (_enh)
                ctx.FamePerRecruit += 1;
        }
    }
}

