using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 暴露选一敌人；群体暴露选去全部城防(0)或全部抗性(1)。仅本场有效。
    /// </summary>
    public sealed class ExposureEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public ExposureEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!ctx.InBattle || ctx.Enemies == null) throw new InvalidOperationException("暴露必须在战斗中施放");
            if (_enh)
            {
                if (option != 0 && option != 1) throw new InvalidOperationException("群体暴露必须选择去城防或去抗性");
            }
            else
            {
                if (option < 0 || option >= ctx.Enemies.Count) throw new InvalidOperationException("暴露必须选择本场战斗中的有效敌人");
                if (ctx.DefeatedEnemyIndices.Contains(option)) throw new InvalidOperationException("目标已经被击败");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var targets = _enh ? Enumerable.Range(0, ctx.Enemies.Count) : new[] { option };
            foreach (int target in targets)
            {
                // Arcane immunity prevents the enemy changes, not this card's separate attack.
                if (ctx.DefeatedEnemyIndices.Contains(target) || ctx.Enemies[target].Abilities.Contains(Ability.MagicResist)) continue;
                if (!_enh || option == 0) ctx.FortificationsRemoved.Add(target);
                if (!_enh || option == 1) ctx.ResistancesRemoved.Add(target);
            }
            ctx.RangedPool += _enh ? 3 : 2;
        }
    }
}
