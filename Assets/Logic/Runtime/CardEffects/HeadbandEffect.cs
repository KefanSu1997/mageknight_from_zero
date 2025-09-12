using MK.Logic.Data;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 啟迪頭環：使用公共技能或永久獲取技能。
    /// option 為技能在供應區中的索引。
    /// </summary>
    public sealed class HeadbandEffect : ICardEffect
    {
        private readonly bool _once;
        public HeadbandEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var supply = ctx.SkillSupply ?? throw new System.InvalidOperationException("缺少技能供應");
            int idx = System.Math.Clamp(option, 0, supply.Offer.Count - 1);
            var skill = supply.Offer[idx];
            if (!_once)
            {
                skill.Effect.Execute(player, ctx);
                if (skill.Reusable)
                    skill.Effect.Execute(player, ctx);
            }
            else
            {
                supply.Offer.RemoveAt(idx);
                player.Skills.Add(skill);
            }
        }
    }
}
