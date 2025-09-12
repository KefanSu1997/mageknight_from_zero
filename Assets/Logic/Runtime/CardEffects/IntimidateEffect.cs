using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 無情威壓：基礎影響力2並招募減免2；強效影響力6，聲望-1。
    /// 重整部隊功能尚未串接。
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
                // 重整部隊需要另行支付影響力，待擴充
            }
        }
    }
}
