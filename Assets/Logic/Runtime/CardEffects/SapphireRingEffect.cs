using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 藍寶石戒指：基礎獲得藍色法力與晶體並+1名望；強效提供無限藍黑法力並施放藍色法術獲得名望。
    /// </summary>
    public sealed class SapphireRingEffect : ICardEffect
    {
        private readonly bool _once;
        public SapphireRingEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                player.Mana.AddToken(ManaColor.Blue, 1);
                player.Mana.AddCrystal(ManaColor.Blue, 1);
                player.Fame += 1;
            }
            else
            {
                ctx.InfiniteMana.Add(ManaColor.Blue);
                ctx.InfiniteMana.Add(ManaColor.Black);
                ctx.FamePerSpell[ManaColor.Blue] = ctx.FamePerSpell.GetValueOrDefault(ManaColor.Blue) + 1;
            }
        }
    }
}
