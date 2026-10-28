using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 学习：先获得影响力和本回合一次可选购牌能力；购牌由CardLearningActions显式执行。
    /// </summary>
    public sealed class LearningEffect : ICardEffect
    {
        private readonly bool _enh;
        public LearningEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += _enh ? 4 : 2;
            ctx.LearningModes |= _enh ? 2 : 1;
        }
    }
}
