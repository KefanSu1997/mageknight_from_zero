using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 靈活戰鬥：基礎三選一，強效六選一，僅處理數值累加。
    /// </summary>
    public sealed class VersatileBattleEffect : ICardEffect, IFixedCardOutput
    {
        private readonly bool _enh;
        public VersatileBattleEffect(bool enhanced) => _enh = enhanced;

        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => !_enh ? new[] {
            new FixedCardOutput(0, FixedOutputKind.Attack, 2), new FixedCardOutput(1, FixedOutputKind.Block, 2),
            new FixedCardOutput(2, FixedOutputKind.Attack, 1, AttackType: AttackType.Ranged) } : new[] {
            new FixedCardOutput(0, FixedOutputKind.Attack, 4), new FixedCardOutput(1, FixedOutputKind.Block, 4),
            new FixedCardOutput(2, FixedOutputKind.Attack, 3, Element.Fire), new FixedCardOutput(3, FixedOutputKind.Block, 3, Element.Fire),
            new FixedCardOutput(4, FixedOutputKind.Attack, 3, AttackType: AttackType.Ranged),
            new FixedCardOutput(5, FixedOutputKind.Attack, 2, AttackType: AttackType.Siege) };
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
            => FixedCardOutputs.Add(ctx, System.Linq.Enumerable.Single(FixedOutputs,o=>o.Option==option));
    }
}
