using System;

namespace MK.Logic.Runtime
{
    /// <summary>宁静时光可选的本回合行动；影响力仍可由其他原卡提供。</summary>
    public static class PeacefulMomentActions
    {
        internal static bool CanAct(ActionContext context, bool ready = false) =>
            context.PeacefulMomentAvailable && (!ready || context.PeacefulMomentReady)
            && !context.InBattle && !context.InNegotiation && !context.InteractionReputationModifier.HasValue
            && !context.TemporalPortalTurn && (!context.TurnActionCommitted || context.PeacefulMomentAction);

        internal static void Commit(ActionContext context) =>
            context.PeacefulMomentAction = context.TurnActionCommitted = true;

        public static void ConvertHealing(PlayerState player, ActionContext context, int points)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (!CanAct(context) || context.HealInfluenceRate <= 0)
                throw new InvalidOperationException("本回合不能执行宁静时光行动");
            if (points <= 0 || points > context.InfluencePool / context.HealInfluenceRate)
                throw new InvalidOperationException("治疗转换数量无效或影响力不足");
            context.InfluencePool -= points * context.HealInfluenceRate;
            context.Healing.Add(points);
            Commit(context);
        }
    }
}
