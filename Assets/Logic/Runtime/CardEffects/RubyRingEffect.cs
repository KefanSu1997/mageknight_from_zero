using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 紅寶石戒指：基礎獲得紅色法力與晶體並+1名望；強效提供無限紅黑法力並施放紅色法術獲得名望。
    /// </summary>
    public sealed class RubyRingEffect : ICardEffect
    {
        private readonly bool _once;
        public RubyRingEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once)
            {
                player.Mana.AddToken(ManaColor.Red, 1);
                player.Mana.AddCrystal(ManaColor.Red, 1);
                player.Fame += 1;
            }
            else
            {
                ctx.InfiniteMana.Add(ManaColor.Red);
                ctx.InfiniteMana.Add(ManaColor.Black);
                ctx.FamePerSpell[ManaColor.Red] = ctx.FamePerSpell.GetValueOrDefault(ManaColor.Red) + 1;
            }
        }
    }
}
