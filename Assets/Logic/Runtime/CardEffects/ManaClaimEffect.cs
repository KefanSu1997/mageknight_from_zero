using MK.Logic.Core;
using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力宣奪 / 魔力詛咒：取得魔力骰並依選擇獲得法力或詛咒其他玩家。
    /// option 低8位為顏色索引，位8~15為 0 表立即獲得3標記，1 表每回合+1。
    /// </summary>
    public sealed class ManaClaimEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ManaClaimEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            ManaSource.ValidateUse(player);
            int color = option & 0xFF;
            int mode = option >> 8;
            if (color < (int)ManaColor.Red || color > (int)ManaColor.White || mode < 0 || mode > 1)
                throw new InvalidOperationException("魔力宣夺必须选择基本色骰及立即或后续回合模式");
            if (ctx.ManaSource == null || ctx.ManaSource.GetAvailableCount((ManaColor)color) == 0)
                throw new InvalidOperationException("魔力源中没有所选基本色骰");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ManaColor color = (ManaColor)(option & 0xFF);
            bool perTurn = (option >> 8) != 0;
            ctx.ManaSource.ClaimForRound(player, color);
            if (perTurn)
            {
                player.TokensPerTurn[color] = player.TokensPerTurn.GetValueOrDefault(color) + 1;
            }
            else
            {
                player.Mana.AddToken(color, 3);
            }
            if (_enh)
            {
                foreach (var other in ctx.OtherPlayers)
                    other.ManaCurseColor = color;
            }
        }
    }
}
