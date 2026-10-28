namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 時光傳送：可瞬移並下次抽牌上限提高。
    /// </summary>
    public sealed class TimeBendingEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public TimeBendingEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > (_enh ? 1 : 0))
                throw new System.InvalidOperationException("时光传送必须选择距离或手牌上限，不能同时强化");
            if (ctx.TurnActionCommitted || ctx.InBattle || ctx.InNegotiation)
                throw new System.InvalidOperationException("时光传送必须作为本回合唯一行动");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.TeleportRange = _enh && option == 0 ? 2 : 1;
            ctx.NextDrawBonus += _enh && option == 1 ? 2 : 1;
            ctx.TemporalPortalPending = true;
            ctx.TemporalPortalTurn = ctx.TurnActionCommitted = true;
            ctx.TeleportMustEndSafe = true;
            ctx.IgnoreRampaging = true;
        }
    }
}
