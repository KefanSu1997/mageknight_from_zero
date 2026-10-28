using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Fear banner: real attachment uses CardPlayActions; burning targets up to three enemies.</summary>
    public sealed class FearBannerEffect : ICardEffect
    {
        private readonly bool _once;
        public FearBannerEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!_once) throw new InvalidOperationException("恐惧旌旗必须通过真实手牌入口附着，再发动所附部队");
            if (!ctx.InBattle || ctx.Enemies == null)
                throw new InvalidOperationException("恐惧旌旗焚毁效果只能用于本场战斗");
            // Empty explicitly selects no enemies; never silently choose the first three.
            var targets = ctx.SpellTargetIndices.ToArray();
            if (targets.Length > 3 || targets.Distinct().Count() != targets.Length
                || targets.Any(i => i < 0 || i >= ctx.Enemies.Count || ctx.DefeatedEnemyIndices.Contains(i)))
                throw new InvalidOperationException("恐惧旌旗必须选择至多三个不同的存活敌人");
            if (targets.Any(i => ctx.Enemies[i].Abilities.Contains(Ability.MagicResist)))
                throw new InvalidOperationException("奥术免疫敌人不能成为恐惧旌旗焚毁效果的目标");
            foreach (int target in targets) ctx.SkipAttackIndices.Add(target);
        }
    }
}
