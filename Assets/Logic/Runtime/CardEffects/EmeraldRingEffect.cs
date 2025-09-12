using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 翡翠戒指：基礎獲得綠色法力與晶體並+1名望；強效提供無限綠黑法力並施放綠色法術獲得名望。
    /// </summary>
    public sealed class EmeraldRingEffect : ICardEffect
    {
        private readonly bool _once;
        public EmeraldRingEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                player.Mana.AddToken(ManaColor.Green, 1);
                player.Mana.AddCrystal(ManaColor.Green, 1);
                player.Fame += 1;
            }
            else
            {
                ctx.InfiniteMana.Add(ManaColor.Green);
                ctx.InfiniteMana.Add(ManaColor.Black);
                ctx.FamePerSpell[ManaColor.Green] = ctx.FamePerSpell.GetValueOrDefault(ManaColor.Green) + 1;
            }
        }
    }
}
