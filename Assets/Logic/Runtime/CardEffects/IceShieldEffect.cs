namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 寒冰之盾：強化時降低一名被格擋敵人的護甲。
    /// 降甲附着在这笔格挡上，实际选择目标且成功格挡后才触发。
    /// </summary>
    public sealed class IceShieldEffect : ICardEffect
    {
        private readonly bool _enh;
        public IceShieldEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.CombatPower.AddBlock(3, MK.Logic.Core.Element.Ice, armorReductionOnSuccess: _enh ? 3 : 0);
        }
    }
}
