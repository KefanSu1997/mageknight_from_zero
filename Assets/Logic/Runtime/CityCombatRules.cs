using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>规则书第10页城市加成。只修饰本场地点的实际守军，不修改印刷标记。</summary>
    public static class CityCombatRules
    {
        private static CityColor Color(Monster enemy, ActionContext context)
        {
            var site = context?.CombatSite;
            return context?.InBattle == true && site?.Type == PlaceType.City
                && site.Defenders.Any(guard => ReferenceEquals(guard, enemy)) ? site.CityColor : CityColor.None;
        }

        public static int ArmorBonus(Monster enemy, ActionContext context)
            => Color(enemy, context) == CityColor.White ? 1 : 0;

        // A temporary calculation profile; never put this copy in a deck, site, or token supply.
        public static Monster DefenseProfile(Monster enemy, ActionContext context, EnemyAttack attack = null)
        {
            var color = Color(enemy, context);
            if (attack != null) enemy = enemy with { Attack = attack.Value, AttackElement = attack.Element };
            if (color == CityColor.Blue)
            {
                int bonus = enemy.AttackElement == Element.ColdFire ? 1
                    : enemy.AttackElement == Element.Fire || enemy.AttackElement == Element.Ice ? 2 : 0;
                return bonus == 0 ? enemy : enemy with { Attack = enemy.Attack + bonus };
            }
            if (enemy.AttackElement == Element.Physical && (color == CityColor.Red || color == CityColor.Green))
            {
                var ability = color == CityColor.Red ? Ability.Brutal : Ability.Poison;
                return enemy.Abilities.Contains(ability) ? enemy : enemy with { Abilities = enemy.Abilities.Append(ability).ToArray() };
            }
            return enemy;
        }
    }
}
