using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 衝鋒陷陣：提升所有部隊攻擊格擋，並禁止部隊受創。
    /// </summary>
    public sealed class ChargeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ChargeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!ctx.InBattle || ctx.CombatPhase != Phase.Ranged || ctx.CombatActionStarted
                || ctx.CombatPower.Attacks.Count > 0 || ctx.CombatPower.Blocks.Count > 0
                || ctx.DefeatedEnemyIndices.Count > 0 || ctx.DamageAssignedUnits.Count > 0)
                throw new System.InvalidOperationException("冲锋陷阵只能在本场战斗开始时打出");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            int val = _enh ? 3 : 2;
            ctx.ChargeUnitBonus += val;
            ctx.NoUnitDamage = true;
        }
    }
}
