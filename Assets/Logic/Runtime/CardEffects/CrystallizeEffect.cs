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
            ManaColor color = (ManaColor)option;
            if (!_enh)
            {
                var cost = new ManaCost(new Dictionary<Element, int>{{color.ToElement(),1}});
                if (player.Mana.Pay(cost, player))
                    player.Mana.AddCrystal(color,1);
            }
            else
            {
                player.Mana.AddCrystal(color,1);
            }
        }
    }
}
