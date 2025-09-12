namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 伏擊：提供移動並強化本回合第一張攻擊或格擋牌。
    /// </summary>
    public sealed class AmbushEffect : ICardEffect
    {
        private readonly bool _enh;
        public AmbushEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += _enh ? 4 : 2;
            ctx.AmbushAttackBonus = _enh ? 2 : 1;
            ctx.AmbushBlockBonus = _enh ? 4 : 2;
            ctx.AmbushTriggered = false;
        }
    }
}
