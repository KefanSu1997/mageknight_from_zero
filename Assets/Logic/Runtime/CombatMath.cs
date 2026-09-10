using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>中文规则书第9、10、24页：所有低效贡献合计后减半，向下取整一次。</summary>
    public static class CombatMath
    {
        public static BlockAllocation[] ResolveBlocks(Monster enemy, IEnumerable<BlockAllocation> blocks)
        {
            // Physical attacks have no color. Cold-fire contains two attack colors.
            int colors = enemy.AttackElement == Element.Physical ? 0 : enemy.AttackElement == Element.ColdFire ? 2 : 1;
            int bonus = enemy.Abilities.Contains(Ability.MagicResist) ? 0 : enemy.Abilities.Count + colors;
            return blocks.Select(b => b.CountEnemySymbols
                ? b with { Value = b.Value + bonus, CountEnemySymbols = false } : b).ToArray();
        }

        public static int EffectiveBlock(Element attack, IEnumerable<BlockAllocation> blocks)
        {
            int full = 0, reduced = 0;
            foreach (var block in blocks)
            {
                if (block.Value < 0) throw new ArgumentOutOfRangeException(nameof(blocks));
                if (Constants.BlockEfficiency(attack, block.Element) == 1) full += block.Value;
                else reduced += block.Value;
            }
            return full + reduced / 2;
        }

        public static bool Resists(Monster enemy, Element element, bool removeFire = false)
        {
            bool fire = !removeFire && enemy.Abilities.Contains(Ability.FireResist);
            bool ice = enemy.Abilities.Contains(Ability.IceResist);
            return element switch
            {
                Element.Physical => enemy.Abilities.Contains(Ability.PhysicalResist),
                Element.Fire => fire,
                Element.Ice => ice,
                Element.ColdFire => (fire && ice) || enemy.Abilities.Contains(Ability.ColdFireResist),
                _ => false
            };
        }

        public static AttackProfile[] Parts(AttackAllocation attack) => attack.Components?.ToArray()
            ?? new[] { new AttackProfile(attack.IsSiege ? AttackType.Siege : AttackType.Ranged, attack.Value, attack.Element) };

        public static bool IsSiege(AttackProfile part, ActionContext ctx)
        {
            bool siege = part.Type == AttackType.Siege;
            if (ctx?.RangedAttackAsSiege == true && part.Type == AttackType.Ranged) siege = true;
            if (ctx?.SiegeAttackAsRanged == true && siege) siege = false;
            return siege;
        }

        public static bool CanAttack(IList<Monster> enemies, IReadOnlyList<int> targets,
            IReadOnlyList<AttackProfile> parts, Phase phase, ActionContext ctx = null)
        {
            if (phase == Phase.Melee) return true;
            if (phase != Phase.Ranged || parts.Any(p => p.Type == AttackType.Melee)) return false;
            if (ctx?.IgnoreFortified == true) return true;
            bool site = ctx?.FortifiedSite == true;
            if (site && targets.Any(i => enemies[i].Abilities.Contains(Ability.Fortified))) return false;
            bool fortified = site || targets.Any(i => enemies[i].Abilities.Contains(Ability.Fortified));
            return !fortified || parts.All(p => IsSiege(p, ctx));
        }

        public static int EffectiveAttack(IList<Monster> enemies, IReadOnlyList<int> targets,
            IEnumerable<AttackProfile> parts, Phase phase, ActionContext ctx = null)
        {
            int full = 0, reduced = 0;
            foreach (var part in parts)
            {
                if (part.Value < 0) throw new ArgumentOutOfRangeException(nameof(parts));
                int value = part.Value;
                if (ctx?.DoublePhysicalAttack == true && part.Element == Element.Physical) value *= 2;
                if (phase == Phase.Ranged)
                {
                    bool siege = IsSiege(part, ctx);
                    if (siege && ctx?.DoubleSiegeAttack == true) value *= 2;
                    if (!siege && ctx?.DoubleRangedAttack == true) value *= 2;
                }
                bool resisted = ctx?.IgnoreResist != true && targets.Any(i =>
                    Resists(enemies[i], part.Element, ctx?.FireResistRemoved.Contains(i) == true));
                if (resisted) reduced += value;
                else full += value;
            }
            return full + reduced / 2;
        }
    }
}
