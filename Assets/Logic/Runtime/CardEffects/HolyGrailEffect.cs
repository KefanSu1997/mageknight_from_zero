using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 黄金圣杯：治療並獲得額外效果。
    /// </summary>
    public sealed class HolyGrailEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public HolyGrailEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (ctx.InBattle) throw new System.InvalidOperationException("战斗中不能使用治疗效果");
            if (option != 0) throw new System.InvalidOperationException("黄金圣杯出牌选项无效");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_once)
            {
                ctx.Healing.Add(2, earnsGrailFame: true);
            }
            else
            {
                ctx.DrawPerHeal += 1;
                ctx.Healing.Add(6);
            }
        }
    }
}
