using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>寒冰护体的敌人符号加成附着在本次格挡贡献上，留待实际选定敌人后计算。</summary>
    public sealed class IcyShellEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public IcyShellEffect(bool enhanced) => _enh = enhanced;
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > (_enh ? 0 : 1)) throw new InvalidOperationException("寒冰护体的效果选项无效");
        }
        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            if (_enh) context.CombatPower.AddBlock(5, Element.Ice, countEnemySymbols: true);
            else if (option == 0) context.CombatPower.AddAttack(2);
            else context.CombatPower.AddBlock(3, Element.Ice);
        }
    }
}
