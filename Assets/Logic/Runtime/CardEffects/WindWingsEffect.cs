namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 清風之翼 / 黑夜之翼：傳送或使多名敵人不攻擊。
    /// option 基礎為移動力數值，強效為目標敵人數量(包含首名)。
    /// </summary>
    public sealed class WindWingsEffect : ICardEffect
    {
        private readonly bool _enh;
        public WindWingsEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.TeleportRange = option;
                ctx.TeleportMustEndSafe = true;
                ctx.IgnoreRampaging = true;
            }
            else
            {
                for (int i = 0; i < option; i++)
                    ctx.SkipAttackIndices.Add(i);
            }
        }
    }
}
