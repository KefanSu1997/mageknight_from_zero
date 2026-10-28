using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>洞悉猎物：选敌标上的一项能力，或所选实际攻击的一种元素。</summary>
    public sealed class InsightPreyEffect : ICardEffect, ICardEffectValidator
    {
        public static IReadOnlyList<(int Option, string Label)> Options(ActionContext ctx)
        {
            var result = new List<(int, string)>();
            try { HeroAttackSkillRules.Target(ctx, false); } catch (InvalidOperationException) { return result; }
            var enemy = ctx.SelectedEnemy;
            if (enemy.Abilities.Contains(Ability.MagicResist)) return result;
            foreach (var ability in enemy.Abilities.Distinct())
            {
                string name = ability switch {
                    Ability.Elusive => "隐匿", Ability.Defend => "防御", Ability.Fortified => "城防", Ability.Swift => "迅捷", Ability.Brutal => "残暴",
                    Ability.Poison => "毒素", Ability.Paralyze => "麻痹", Ability.Assassination => "刺杀",
                    Ability.FireResist => "火焰抗性", Ability.IceResist => "寒冰抗性",
                    Ability.ColdFireResist => "冰火抗性", Ability.PhysicalResist => "物理抗性", _ => null };
                if (name != null && !ctx.InsightIgnores(enemy, ability)) result.Add((100 + (int)ability, "忽略" + name));
            }
            try { HeroAttackSkillRules.Target(ctx, true); } catch (InvalidOperationException) { return result; }
            var element = CardCombatActions.DefenseProfile(enemy, Enumerable.Range(0, ctx.Enemies.Count).First(i => ReferenceEquals(ctx.Enemies[i], enemy)), ctx, ctx.SelectedEnemyAttackIndex).AttackElement;
            if (element == Element.Fire || element == Element.Ice) result.Add((200 + (int)Element.Physical, "去色：物理"));
            if (element == Element.ColdFire) { result.Add((200 + (int)Element.Fire, "去冰：火焰")); result.Add((200 + (int)Element.Ice, "去火：寒冰")); }
            return result;
        }
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!Options(ctx).Any(x => x.Option == option)) throw new InvalidOperationException("请选择存活敌标的实际能力或可去除的攻击元素；奥术免疫不可选");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option < 200) ctx.AddInsightAbility(ctx.SelectedEnemy, (Ability)(option - 100));
            else ctx.AddInsightElement(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex, (Element)(option - 200));
        }
    }
}
