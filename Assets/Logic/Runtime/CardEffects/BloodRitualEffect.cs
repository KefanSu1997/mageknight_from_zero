using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Take a real wound; choose each token and optionally crystallize one basic-color mana.</summary>
    public sealed class BloodRitualEffect : ICardEffect, ICardEffectValidator, ICardContextualManaCost
    {
        private readonly bool _enh;
        public BloodRitualEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 5)
                throw new InvalidOperationException("鲜血仪式必须选择有效魔力颜色");
            if (!_enh) return;
            if (ctx.BloodRitualColors.Count != 0 &&
                (ctx.BloodRitualColors.Count != 3 || ctx.BloodRitualColors.Any(c => (int)c < 0 || (int)c > 5)))
                throw new InvalidOperationException("鲜血仪式强化必须分别选择三枚魔力颜色");
            if (ctx.BloodRitualCrystalColor is ManaColor crystal && ((int)crystal < 0 || (int)crystal > 3))
                throw new InvalidOperationException("鲜血仪式只能转换红蓝绿白魔晶");
        }

        private ManaColor[] Tokens(ActionContext ctx, int option) =>
            ctx.BloodRitualColors.Count == 0
                ? new[] { (ManaColor)option, (ManaColor)option, (ManaColor)option }
                : ctx.BloodRitualColors.ToArray();

        public ManaColor[] GetAdditionalManaCost(PlayerState player, ActionContext ctx, int option)
        {
            // A color not produced by this card must be affordable together with the printed fee.
            if (_enh && ctx.BloodRitualCrystalColor is ManaColor color && !Tokens(ctx, option).Contains(color))
                return new[] { color };
            return Array.Empty<ManaColor>();
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            player.Wounds += 1;
            if (!_enh)
            {
                player.Mana.AddCrystal(ManaColor.Red, 1);
                player.Mana.AddToken((ManaColor)option, 1);
            }
            else
            {
                var tokens = Tokens(ctx, option);
                foreach (var color in tokens) player.Mana.AddToken(color, 1);
                if (ctx.BloodRitualCrystalColor is ManaColor crystal)
                {
                    // New mana is paid here; any other color was paid atomically above.
                    if (tokens.Contains(crystal) && !player.Mana.TryPayExactColors(new[] { crystal }, ctx, player))
                        throw new InvalidOperationException("鲜血仪式新产生的转晶魔力不足");
                    player.Mana.AddCrystal(crystal, 1);
                }
            }
            ctx.BloodRitualColors.Clear();
            ctx.BloodRitualCrystalColor = null;
        }
    }
}
