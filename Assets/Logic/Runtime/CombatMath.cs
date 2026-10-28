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

        public static int EffectiveArmor(Monster enemy, int index, ActionContext ctx = null, Phase? phase = null, int additionalDefend = 0)
        {
            if (ctx?.BlockedArmorOne == true && ctx.WasBlockedThisBattle(enemy)
                && !enemy.Abilities.Contains(Ability.MagicResist)) return 1;
            bool low = (phase ?? ctx?.CombatPhase) == Phase.Melee && ctx != null
                && Enumerable.Range(0, enemy.AttackCount).All(a => ctx.SuccessfullyBlockedAttacks.Contains((index, a)));
            int printed = enemy.ElusiveArmor > 0 && enemy.Abilities.Contains(Ability.Elusive)
                && ctx?.InsightIgnores(enemy, Ability.Elusive) != true && !low ? enemy.ElusiveArmor : enemy.Armor;
            int armor = printed + (ctx?.Vampirism.Armor(enemy) ?? 0) + CityCombatRules.ArmorBonus(enemy, ctx) + (ctx?.EnemyDefense.Bonus(enemy) ?? 0) + additionalDefend;
            int reduction = ctx?.ArmorReduction.GetValueOrDefault(index) ?? 0;
            reduction += ctx?.ExplosiveBoltArmorReduction(enemy) ?? 0;
            reduction += ctx?.SkillArmorReduction(enemy) ?? 0;
            reduction += ctx?.UnitArmorReduction(enemy) ?? 0;
            return reduction > 0 ? Math.Min(armor, Math.Max(1, armor - reduction)) : armor;
        }

        public static int EffectiveBlock(Element attack, IEnumerable<BlockAllocation> blocks, bool swift = false)
        {
            int full = 0, reduced = 0;
            foreach (var block in blocks)
            {
                if (block.Value < 0) throw new ArgumentOutOfRangeException(nameof(blocks));
                int value = block.Value * (swift && block.DoubleAgainstSwift ? 2 : 1);
                if (Constants.BlockEfficiency(attack, block.Element) == 1) full += value;
                else reduced += value;
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

        /// <summary>敌人当前有效抗性；供正式攻击与红／蓝非攻击效果共同读取。</summary>
        public static bool HasResistance(Monster enemy, int index, Element element, ActionContext ctx = null)
            => ctx?.ResistancesRemoved.Contains(index) != true
                && Resists(ctx == null ? enemy : enemy with { Abilities = enemy.Abilities.Where(a => !ctx.InsightIgnores(enemy, a)).ToArray() }, element, ctx?.FireResistRemoved.Contains(index) == true);

        /// <summary>敌人当前是否仍有任一层城防；地点层与自身层均考虑本次有效移除。</summary>
        public static bool IsFortified(Monster enemy, int index, ActionContext ctx = null)
            => ctx?.IgnoreFortified != true && ctx?.FortificationsRemoved.Contains(index) != true
                && ((enemy.Abilities.Contains(Ability.Fortified) && ctx?.InsightIgnores(enemy, Ability.Fortified) != true) || HasSiteFortification(enemy,ctx));

        private static bool HasSiteFortification(Monster enemy, ActionContext ctx)
            => ctx?.FortifiedSite == true && !ctx.IgnoreSiteFortifications
                && !enemy.Abilities.Contains(Ability.IgnoreSiteFortification)
                && (ctx.CombatSite == null || ctx.CombatSite.Defenders.Any(e=>ReferenceEquals(e,enemy)));

        /// <summary>仅本次实际使用的原卡贡献可对本次击杀发放印刷奖励。</summary>
        public static (int Fame, int Reputation) AttackRewards(IEnumerable<AttackProfile> parts, int kills)
        {
            if (kills <= 0) return (0, 0);
            var used = parts.ToArray();
            return (used.Sum(p => p.FamePerDefeatedEnemy) * kills + used.Sum(p => p.FameIfAnyDefeated),
                used.Sum(p => p.ReputationPerDefeatedEnemy) * kills);
        }

        public static bool IsSiege(AttackProfile part, ActionContext ctx)
        {
            if (ctx?.AllAttacksSiege == true) return true;
            if (ctx?.StarBowActive == true)
            {
                if (part.Type == AttackType.Ranged && part.StarBowChoice == 2) return true;
                if (part.Type == AttackType.Siege && part.StarBowChoice == 1) return false;
            }
            bool siege = part.Type == AttackType.Siege;
            if (ctx?.RangedAttackAsSiege == true && part.Type == AttackType.Ranged) siege = true;
            if (ctx?.SiegeAttackAsRanged == true && siege) siege = false;
            return siege;
        }

        public static bool CanAttack(IList<Monster> enemies, IReadOnlyList<int> targets,
            IReadOnlyList<AttackProfile> parts, Phase phase, ActionContext ctx = null)
        {
            if (parts.Any(p => p.RequiredEnemy != null && !targets.Any(i => ReferenceEquals(enemies[i], p.RequiredEnemy)))) return false;
            if (parts.Any(p => p.HasExcludedTarget &&
                (phase != Phase.Melee || targets.Any(i => p.Excludes(enemies[i], i))))) return false;
            if (phase == Phase.Melee) return true;
            if (phase != Phase.Ranged || ctx?.AllAttacksSiege != true && parts.Any(p => p.Type == AttackType.Melee)) return false;
            if (ctx?.IgnoreFortified == true) return true;
            var protectedTargets = targets.Where(i => ctx?.FortificationsRemoved.Contains(i) != true && !enemies[i].Abilities.Contains(Ability.IgnoreSiteFortification)).ToArray();
            if (protectedTargets.Any(i => HasSiteFortification(enemies[i],ctx) && enemies[i].Abilities.Contains(Ability.Fortified) && ctx?.InsightIgnores(enemies[i], Ability.Fortified) != true)) return false;
            bool fortified = targets.Any(i => IsFortified(enemies[i], i, ctx));
            return !fortified || parts.All(p => IsSiege(p, ctx));
        }

        /// <summary>实际贡献值（尚未按目标抗性折减），UI与结算共同使用。</summary>
        public static int AttackValue(AttackProfile part, Phase phase, ActionContext ctx = null)
        {
            int value = part.Value + (ctx?.OtherUnitAttackBonus(part.SourceUnit) ?? 0);
            if (ctx?.DoublePhysicalAttack == true && phase == Phase.Melee && (ctx?.AllAttacksColdFire == true ? Element.ColdFire : part.Element) == Element.Physical) value *= 2;
            if (ctx?.StarBowActive == true && (part.Type == AttackType.Ranged && part.StarBowChoice != 2
                || part.Type == AttackType.Siege && part.StarBowChoice == 1)) value *= 2;
            if (phase == Phase.Ranged)
            {
                bool siege = IsSiege(part, ctx);
                if (siege && ctx?.DoubleSiegeAttack == true) value *= 2;
                if (!siege && ctx?.DoubleRangedAttack == true) value *= 2;
            }
            return value;
        }

        public static int EffectiveAttack(IList<Monster> enemies, IReadOnlyList<int> targets,
            IEnumerable<AttackProfile> parts, Phase phase, ActionContext ctx = null)
        {
            int unrestrictedHalfPoints = 0;
            var restrictedHalfPoints = new Dictionary<int, int>();
            foreach (var part in parts)
            {
                if (part.Value < 0) throw new ArgumentOutOfRangeException(nameof(parts));
                int target = part.RequiredEnemy == null ? -1 : targets.Where(i => ReferenceEquals(enemies[i], part.RequiredEnemy)).DefaultIfEmpty(-1).First();
                if (part.RequiredEnemy != null && target < 0) continue;
                int value = AttackValue(part, phase, ctx);
                var element = ctx?.AllAttacksColdFire == true ? Element.ColdFire : part.Element;
                bool resisted = ctx?.IgnoreResist != true && !(ctx?.IgnorePhysicalResist == true && element == Element.Physical) && targets.Any(i =>
                    HasResistance(enemies[i], i, element, ctx));
                int halfPoints = value * (resisted ? 1 : 2);
                if (target < 0) unrestrictedHalfPoints += halfPoints;
                else restrictedHalfPoints[target] = restrictedHalfPoints.GetValueOrDefault(target) + halfPoints;
            }
            // A targeted contribution can only cover its enemy's armor. Keep halves until
            // this entire attack is totaled, just as for ordinary resisted contributions.
            int usableHalfPoints = unrestrictedHalfPoints + restrictedHalfPoints.Sum(pair =>
                Math.Min(pair.Value, 2 * EffectiveArmor(enemies[pair.Key], pair.Key, ctx, phase)));
            return usableHalfPoints / 2;
        }
    }
}
