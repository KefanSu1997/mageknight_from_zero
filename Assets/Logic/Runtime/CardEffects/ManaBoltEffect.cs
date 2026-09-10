using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>ActionSystem pays the printed blue cost plus the chosen color before applying this effect.</summary>
    public sealed class ManaBoltEffect : ICardEffect, ICardEffectValidator, ICardAdditionalManaCost
    {
        private readonly bool _enh;
        public ManaBoltEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 3) throw new InvalidOperationException("魔力箭矢必须选择红蓝绿白之一");
        }

        public ManaColor[] GetAdditionalManaCost(int option) => new[] { (ManaColor)option };

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var color = (ManaColor)option;
            int value = color switch { ManaColor.Blue => 8, ManaColor.Red => 7, ManaColor.White => 6, _ => 5 };
            var type = color == ManaColor.White ? AttackType.Ranged : color == ManaColor.Green ? AttackType.Siege : AttackType.Melee;
            ctx.CombatPower.AddAttack(value + (_enh ? 3 : 0), color == ManaColor.Red ? Element.ColdFire : Element.Ice, type);
        }
    }
}
