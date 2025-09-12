using MK.Logic.Core;
using System.Collections.Generic;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魔力宣奪 / 魔力詛咒：取得魔力骰並依選擇獲得法力或詛咒其他玩家。
    /// option 低8位為顏色索引，位8~15為 0 表立即獲得3標記，1 表每回合+1。
    /// </summary>
    public sealed class ManaClaimEffect : ICardEffect
    {
        private readonly bool _enh;
        public ManaClaimEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)(option & 0xFF);
            bool perTurn = (option >> 8) != 0;
            var source = ctx.ManaSource ?? throw new System.InvalidOperationException("缺少魔力源");
            try
            {
                source.Take(player, color);
            }
            catch
            {
                source.TakeAny(player);
            }
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
