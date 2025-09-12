using MK.Logic.Core;
using System.Collections.Generic;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 歡悅魔晶：消耗或獲得魔力晶體，回合結束可回手（未實作）。
    /// </summary>
    public sealed class JoyfulManaEffect : ICardEffect
    {
        private readonly bool _enh;
        public JoyfulManaEffect(bool enhanced) => _enh = enhanced;


        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            if (!_enh)
            {
                var cost = new ManaCost(new Dictionary<Element,int>{{color.ToElement(),1}});
                if (player.Mana.Pay(cost, player))
                    player.Mana.AddCrystal(color,1);
            }
            else
            {
                player.Mana.AddCrystal(color,1);
            }
            // 回合結束回手效果留待流程系統實作
        }
    }
}
