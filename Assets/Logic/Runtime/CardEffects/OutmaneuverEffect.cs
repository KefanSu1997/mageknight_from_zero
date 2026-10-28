using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 迂回躲避：降低敵人攻擊，若在攻擊階段前未受創則增加攻擊力。
    /// option=0 強效降低4，option=1 兩次降低2。
    /// </summary>
    public sealed class OutmaneuverEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public OutmaneuverEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > (_enh ? 1 : 0))
                throw new InvalidOperationException("迂回躲避必须选择一次减攻或强化的两次减攻");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.AttackReduction += 2;
                ctx.AttackIfNoWound = 1;
                ctx.AttackReductionTimes = 1;
            }
            else
            {
                if (option == 0)
                {
                    ctx.AttackReduction += 4;
                    ctx.AttackReductionTimes = 1;
                }
                else
                {
                    ctx.AttackReduction += 2;
                    ctx.AttackReductionTimes = 2;
                }
                ctx.AttackIfNoWound = 2;
            }
            ctx.ResolveNoWoundAttack();
        }
    }
}
