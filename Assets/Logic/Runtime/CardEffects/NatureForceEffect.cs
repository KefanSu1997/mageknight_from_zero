using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 自然之力：基礎賦予部隊物理抗性，強效提供攻城或格擋。
    /// option 0 攻城3，1 格擋6
    /// </summary>
    public sealed class NatureForceEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public NatureForceEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > (_enh ? 1 : 0)) throw new System.InvalidOperationException("自然之力选项无效");
            if (!_enh && (ctx.TargetUnit == null || !player.Units.Contains(ctx.TargetUnit) || ctx.TargetUnit.IsDestroyed))
                throw new System.InvalidOperationException("自然之力必须选择自有且未销毁的部队");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                if (ctx.TargetUnit != null)
                    ctx.TargetUnit.PhysicalResistTemp = true;
            }
            else
            {
                if (option == 0)
                    ctx.CombatPower.AddAttack(3, Element.Physical, AttackType.Siege);
                else
                    ctx.BlockPool += 6;
            }
        }
    }
}
