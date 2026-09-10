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
            // 回合結束回手效果留待流程系統實作
        }
    }
}
