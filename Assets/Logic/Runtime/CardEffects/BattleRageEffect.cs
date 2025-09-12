using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 戰鬥之怒：基礎攻擊2並在受傷時額外+1；強效攻擊4並每受傷+1，最多+4。
    /// 加值效果將由 BattleResolver 在分配傷害後處理。
    /// </summary>
    public sealed class BattleRageEffect : ICardEffect
    {
        private readonly bool _enh;
        public BattleRageEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.MeleePool += 2;
                ctx.AttackBonusPerWound = 1;
                ctx.AttackBonusLimit = 1;
            }
            else
            {
                ctx.MeleePool += 4;
                ctx.AttackBonusPerWound = 1;
                ctx.AttackBonusLimit = 4;
            }
        }
    }
}
