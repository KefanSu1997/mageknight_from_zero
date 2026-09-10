using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 無情威壓：基礎影響力2並招募減免2；強效影響力6，聲望-1。
    /// 強效的重整許可由CardUnitActions支付每級2影響力，限自有1、2級部隊。
    /// </summary>
    public sealed class IntimidateEffect : ICardEffect
    {
        private readonly bool _enh;
        public IntimidateEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.InfluencePool += 2;
                ctx.RecruitDiscount += 2;
                ctx.IntimidateUsed = true;
            }
            else
            {
                ctx.InfluencePool += 6;
                player.Reputation -= 1;
                ctx.ReadyInfluencePerLevel = 2;
                ctx.ReadyUnitMaxLevel = System.Math.Max(ctx.ReadyUnitMaxLevel, 2);
            }
        }
    }
}
