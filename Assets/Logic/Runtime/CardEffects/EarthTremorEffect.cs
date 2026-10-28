using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 地脈震顫 / 地動山搖：降低敵人護甲。
    /// option >=0 指定目標索引，-1 表示全部敵人。
    /// 需要於 <see cref="ActionContext.Enemies"/> 提供敵人列表以判斷城防。
    /// </summary>
    public sealed class EarthTremorEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public EarthTremorEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!ctx.InBattle || ctx.Enemies == null) throw new InvalidOperationException("震颤必须在战斗中施放");
            if (option < -1 || option >= ctx.Enemies.Count) throw new InvalidOperationException("震颤必须选择有效敌人或全体模式");
            if (option >= 0)
            {
                if (ctx.DefeatedEnemyIndices.Contains(option)) throw new InvalidOperationException("目标已经被击败");
                if (ctx.Enemies[option].Abilities.Contains(Ability.MagicResist))
                    throw new InvalidOperationException("奥术免疫敌人不能成为震颤目标");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = -1)
        {
            Validate(player, ctx, option);
            bool all = option == -1;
            var targets = all ? Enumerable.Range(0, ctx.Enemies.Count) : new[] { option };
            foreach (int i in targets)
            {
                if (ctx.DefeatedEnemyIndices.Contains(i) || ctx.Enemies[i].Abilities.Contains(Ability.MagicResist)) continue;
                int reduction = all ? 2 : 3;
                if (_enh && CombatMath.IsFortified(ctx.Enemies[i], i, ctx)) reduction *= 2;
                ctx.ArmorReduction.TryGetValue(i, out int previous);
                ctx.ArmorReduction[i] = previous + reduction;
            }
        }
    }
}
