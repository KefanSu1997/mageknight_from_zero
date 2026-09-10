using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 晶化：將法力轉化為同色或任意色晶體。
    /// </summary>
    public sealed class CrystallizeEffect : ICardEffect
    {
        private readonly bool _enh;
        public CrystallizeEffect(bool enhanced) => _enh = enhanced;


        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option < 0 || option > 3) throw new System.ArgumentOutOfRangeException(nameof(option));
            ManaColor color = (ManaColor)option;
            if (!_enh)
            {
                if (!player.Mana.TryPayExactColors(new[] { color }, ctx, player))
                    throw new System.InvalidOperationException("法力不足，不能晶化");
                player.Mana.AddCrystal(color,1);
            }
            else
            {
                player.Mana.AddCrystal(color,1);
            }
        }
    }
}
