using System;
using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>把一个实际金色魔力标记用于玩家选定的基础颜色，不创造魔晶或黑色魔力。</summary>
    public static class ManaTokenActions
    {
        public static void ConvertGold(PlayerState player, ActionContext context, ManaColor color)
        {
            if (color < ManaColor.Red || color > ManaColor.White)
                throw new InvalidOperationException("金色魔力只能用作红蓝绿白四种基础颜色");
            if (context.DayPart == DayPart.Night && !context.AllowGoldAtNight)
                throw new InvalidOperationException("黑夜不能使用金色魔力");
            int count = player.Mana.Tokens.GetValueOrDefault(ManaColor.Gold);
            if (count <= 0) throw new InvalidOperationException("没有可用的金色魔力标记");
            if (count == 1) player.Mana.Tokens.Remove(ManaColor.Gold);
            else player.Mana.Tokens[ManaColor.Gold] = count - 1;
            player.Mana.AddToken(color);
        }
    }
}
