using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 鑽石戒指：基礎獲得白色法力與晶體並+1名望；強效提供無限白黑法力並施放白色法術獲得名望。
    /// </summary>
    public sealed class DiamondRingEffect : ICardEffect
    {
        private readonly bool _once;
        public DiamondRingEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                player.Mana.AddToken(ManaColor.White, 1);
                player.Mana.AddCrystal(ManaColor.White, 1);
                player.Fame += 1;
            }
            else
            {
                ctx.InfiniteMana.Add(ManaColor.White);
                ctx.InfiniteMana.Add(ManaColor.Black);
                ctx.FamePerSpell[ManaColor.White] = ctx.FamePerSpell.GetValueOrDefault(ManaColor.White) + 1;
            }
        }
    }
}
