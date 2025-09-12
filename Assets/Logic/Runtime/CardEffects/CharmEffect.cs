using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魅惑 / 附身：影響力或奪取敵人攻擊力。
    /// option 低8位為顏色(基礎選擇水晶時)，高8位 1 表減招募費。
    /// </summary>
    public sealed class CharmEffect : ICardEffect
    {
        private readonly bool _enh;
        public CharmEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_enh)
            {
                ctx.InfluencePool += 4;
                if (ctx.InNegotiation)
                {
                    bool reduce = (option >> 8) != 0;
                    ManaColor color = (ManaColor)(option & 0xFF);
                    if (reduce)
                        ctx.RecruitDiscount += 3;
                    else
                        player.Mana.AddCrystal(color,1);
                }
            }
            else
            {
                ctx.SkipAttackIndices.Add(option);
                ctx.MeleePool += 5; // 簡化處理
            }
        }
    }
}
