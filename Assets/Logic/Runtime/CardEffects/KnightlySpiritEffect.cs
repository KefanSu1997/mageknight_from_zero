namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 騎士精神：攻擊並依擊殺獲得聲望/名望。
    /// option 0 普通攻擊，1 帶加值效果。
    /// </summary>
    public sealed class KnightlySpiritEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public KnightlySpiritEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 1) throw new System.InvalidOperationException("骑士精神必须选择普通攻击或附带奖励的攻击");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0)
            {
                ctx.MeleePool += _enh ? 6 : 3;
            }
            else
            {
                // Reward belongs to this card's contribution to the selected attack.
                ctx.CombatPower.AddAttack(_enh ? 4 : 2, famePerDefeatedEnemy: _enh ? 1 : 0, reputationPerDefeatedEnemy: 1);
            }
        }
    }
}
