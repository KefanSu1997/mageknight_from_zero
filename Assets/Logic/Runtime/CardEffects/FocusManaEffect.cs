using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>全神贯注基础效果：蓝、白、红魔力，或绿色魔晶。</summary>
    public sealed class FocusManaEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext context, int option)
        {
            if (option < 0 || option > 3) throw new InvalidOperationException("全神贯注的魔力选项无效");
        }
        public void Execute(PlayerState player, ActionContext context, int option = 0)
        {
            Validate(player, context, option);
            if (option == 3) player.Mana.AddCrystal(ManaColor.Green);
            else player.Mana.AddToken(new[] { ManaColor.Blue, ManaColor.White, ManaColor.Red }[option]);
        }
    }
}
