using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 戰鬥之怒：基礎攻擊2並在受傷時額外+1；強效攻擊4並每受傷+1，最多+4。
    /// 加值以本场实际入手伤牌累计结算，涵盖出牌前后，整场只使用一次上限。
    /// </summary>
    public sealed class BattleRageEffect : ICardEffect, IFixedCardOutput
    {
        private readonly bool _enh;
        public BattleRageEffect(bool enhanced) => _enh = enhanced;

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => new[] { new FixedCardOutput(0, FixedOutputKind.Attack, _enh ? 4 : 2) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.ClearBattleRage();
            if (!_enh)
            {
                FixedCardOutputs.Add(ctx, FixedOutputs[0]);
                ctx.AttackBonusPerWound = 1;
                ctx.AttackBonusLimit = 1;
            }
            else
            {
                FixedCardOutputs.Add(ctx, FixedOutputs[0]);
                ctx.AttackBonusPerWound = 1;
                ctx.AttackBonusLimit = 4;
            }
            ctx.ApplyBattleRageReward();
        }
    }
}
