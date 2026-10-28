namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 利斧投擲：基礎可移動或遠攻，強效遠攻且若擊殺敵人獲得名望。
    /// </summary>
    public sealed class ThrowingAxeEffect : ICardEffect, IFixedCardOutput, ICardEffectValidator
    {
        private readonly bool _enh;
        public ThrowingAxeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > (_enh ? 0 : 1)) throw new System.InvalidOperationException("利斧投掷选项无效");
        }

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => _enh
            ? new[] { new FixedCardOutput(0, FixedOutputKind.Attack, 3, AttackType: MK.Logic.Core.AttackType.Ranged) }
            : new[] { new FixedCardOutput(0, FixedOutputKind.Movement, 2), new FixedCardOutput(1, FixedOutputKind.Attack, 1, AttackType: MK.Logic.Core.AttackType.Ranged) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                if (option == 0)
                    FixedCardOutputs.Add(ctx, FixedOutputs[0]);
                else
                    FixedCardOutputs.Add(ctx, FixedOutputs[1]);
            }
            else
            {
                if (ctx.ShapeshiftTarget == null) ctx.CombatPower.AddAttack(3, type: MK.Logic.Core.AttackType.Ranged, fameIfAnyDefeated: 1);
                else FixedCardOutputs.Add(ctx, FixedOutputs[0]);
            }
        }
    }
}
