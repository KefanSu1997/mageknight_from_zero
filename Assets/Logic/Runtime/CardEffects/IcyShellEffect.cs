using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>寒冰护体的敌人符号加成附着在本次格挡贡献上，留待实际选定敌人后计算。</summary>
    public sealed class IcyShellEffect : ICardEffect, ICardEffectValidator, IFixedCardOutput
    {
        private readonly bool _enh;
        public IcyShellEffect(bool enhanced) => _enh = enhanced;
        public System.Collections.Generic.IReadOnlyList<FixedCardOutput> FixedOutputs => _enh
            ? new[] { new FixedCardOutput(0, FixedOutputKind.Block, 5, Element.Ice) }
            : new[] { new FixedCardOutput(0, FixedOutputKind.Attack, 2), new FixedCardOutput(1, FixedOutputKind.Block, 3, Element.Ice) };
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > (_enh ? 0 : 1)) throw new InvalidOperationException("寒冰护体的效果选项无效");
        }
        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            if (_enh && context.ShapeshiftTarget == null) context.CombatPower.AddBlock(5, Element.Ice, countEnemySymbols: true);
            else
            {
                FixedCardOutputs.Add(context, FixedOutputs[option]);
                if (_enh) context.CombatPower.AddBlock(0, Element.Ice, countEnemySymbols: true);
            }
        }
    }
}
