using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>卡牌产生的战斗资源经选择目标后交给正式结算器；合法但不足的攻击仍消耗资源。</summary>
    public static class CardCombatActions
    {
        /// <summary>Apply the red secondary effect to another actual surviving enemy, or decline the remainder.</summary>
        public static void ResolveExplosiveBoltArmor(ActionContext context, Monster target)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (!context.InBattle || context.ExplosiveBoltArmorUses <= 0)
                throw new InvalidOperationException("没有本次爆裂箭矢击杀产生的减甲次数");
            if (target == null) { context.ExplosiveBoltArmorUses = 0; return; }
            int index = context.Enemies == null ? -1 : Enumerable.Range(0, context.Enemies.Count)
                .Where(i => ReferenceEquals(context.Enemies[i], target)).DefaultIfEmpty(-1).First();
            if (index < 0 || context.DefeatedEnemyIndices.Contains(index))
                throw new InvalidOperationException("必须选择本场另一名存活的敌人");
            if (target.Abilities.Contains(Ability.MagicResist) || CombatMath.HasResistance(target, index, Element.Fire, context))
                throw new InvalidOperationException("目标不受红色减甲效果影响");
            context.AddExplosiveBoltArmorReduction(target);
            context.ExplosiveBoltArmorUses--;
        }

        internal static void RequireExplosiveBoltArmorResolved(ActionContext context)
        {
            if (context?.ExplosiveBoltArmorUses > 0)
                throw new InvalidOperationException("请先选择或放弃爆裂箭矢击杀后的减甲");
        }

        public static void ReduceEnemyAttack(ActionContext context, int target, int attackIndex = 0)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (!context.InBattle || context.Enemies == null || target < 0 || target >= context.Enemies.Count)
                throw new InvalidOperationException("减攻必须选择本场战斗中的有效敌人");
            if (context.DefeatedEnemyIndices.Contains(target)) throw new InvalidOperationException("目标已经被击败");
            if (attackIndex < 0 || attackIndex >= context.Enemies[target].AttackCount)
                throw new InvalidOperationException("敌人攻击序号无效");
            if (context.AttackReduction <= 0 || context.AttackReductionTimes <= 0)
                throw new InvalidOperationException("没有可用的减攻次数");
            // Lost Legion p5: arcane immunity protects the enemy, not an effect targeting its attack.
            if (context.PendingAttackReductions.ContainsKey((target, attackIndex)))
                throw new InvalidOperationException("这次攻击已经分配了减攻；两次减攻必须指定不同攻击");
            context.PendingAttackReductions.Add((target, attackIndex), context.AttackReduction);
            context.AttackReductionTimes--;
            if (context.AttackReductionTimes == 0) context.AttackReduction = 0;
            context.TurnActionCommitted = true;
        }

        public static Monster DefenseProfile(Monster enemy, int target, ActionContext context, int attackIndex = 0)
        {
            var attack = enemy.GetAttack(attackIndex);
            var profile = CityCombatRules.DefenseProfile(enemy, context, attack);
            if (context != null) profile = profile with {
                Abilities = profile.Abilities.Where(a => !context.InsightIgnores(enemy, a)).ToArray(),
                AttackElement = context.InsightElement(enemy, attackIndex, profile.AttackElement)
            };
            if (context?.ColdStareSuppresses(enemy, attackIndex) == true)
                profile = profile with { Abilities = profile.Abilities.Where(a => a != Ability.Swift
                    && a != Ability.Brutal && a != Ability.Poison && a != Ability.Paralyze && a != Ability.Assassination
                    && a != Ability.Vampirism && a != Ability.Cumbersome).ToArray() };
            if (context != null) profile = profile with { Attack = Math.Max(0, profile.Attack + context.SkillAttackAdjustment(enemy, attackIndex) - NatureRevengeActions.Reduction(context, enemy, attackIndex)) };
            return context != null && context.PendingAttackReductions.TryGetValue((target, attackIndex), out int reduction)
                ? profile with { Attack = Math.Max(0, profile.Attack - reduction) } : profile;
        }

        public static void ChooseStarBow(ActionContext context, int contribution, int choice)
        {
            if (!context.StarBowActive) throw new InvalidOperationException("星辰之弓焚毁效果未生效");
            context.CombatPower.SetStarBowChoice(contribution, choice);
        }

        public static void EnterPhase(ActionContext context, Phase phase)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (context.CombatPhase != phase)
            {
                if (phase == Phase.Block && context.Enemies != null && context.Enemies.Any(e => e.SummonAttack))
                    context.Summoning.ReplaceDefenders(context);
                if (phase == Phase.Melee && context.Summoning.IsActive)
                    context.Summoning.RestoreSummoners(context);
                context.BowFamePerKill = 0;
                context.CrystalsPerKill = 0;
                context.ClearKillCrystalChoices();
            }
            context.CombatPhase = phase;
            DuelActions.EnterPhase(context, phase);
            context.ResolveNoWoundAttack();
        }

        public sealed record Result(int PrintedPower, int EffectivePower, int RequiredPower, int Kills, int Fame, int Wounds);

        public static Result Attack(PlayerState player, ActionContext context, IList<Monster> enemies,
            IReadOnlyList<int> targets, Phase phase, IReadOnlyList<AttackProfile> selected = null)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            RequireExplosiveBoltArmorResolved(context);
            if (context.TemporalPortalTurn) throw new InvalidOperationException("时光传送已占用本回合行动");
            if (context.PeacefulMomentAction) throw new InvalidOperationException("宁静时光已占用本回合行动");
            if (phase != Phase.Ranged && phase != Phase.Melee) throw new InvalidOperationException("当前不是攻击阶段");
            if (targets == null || targets.Count == 0 || targets.Distinct().Count() != targets.Count ||
                targets.Any(i => i < 0 || i >= enemies.Count)) throw new InvalidOperationException("攻击目标无效");
            if (targets.Any(context.DefeatedEnemyIndices.Contains)) throw new InvalidOperationException("目标已经被击败");
            var available = context.CombatPower.AvailableAttacks(phase, targets, enemies, context.AllAttacksSiege);
            var parts = selected == null ? available : selected.ToArray();
            // Contributions are actual objects, not interchangeable records with equal values.
            // Validate the whole selection before spending anything or committing enemy Defend.
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] == null || !available.Any(a => ReferenceEquals(a, parts[i]))
                    || parts.Take(i).Any(a => ReferenceEquals(a, parts[i])))
                    throw new InvalidOperationException("请选择尚未消费、适用于目标且不重复的实际攻击贡献");
            if (parts.Length == 0) throw new InvalidOperationException("没有可用于此阶段的攻击");
            if (!CombatMath.CanAttack(enemies, targets, parts, phase, context))
                throw new InvalidOperationException("城防限制：当前攻击不能指定这些目标");
            var pendingDefend = context.EnemyDefense.PreviewAssignments(enemies, targets, context);
            int required = targets.Sum(i => CombatMath.EffectiveArmor(enemies[i], i, context, phase,
                pendingDefend.Where(a => ReferenceEquals(a.Target, enemies[i])).Sum(a => a.Value)));
            int effective = CombatMath.EffectiveAttack(enemies, targets, parts, phase, context);
            // Rulebook p9.6: insufficient strength is still an editable attack proposal.
            // Do not spend its contributions or commit provisional enemy Defend bonuses.
            if (effective < required)
                throw new InvalidOperationException($"攻击不足：有效{effective} / 护甲{required}。可追加卡牌或改选目标，攻击来源尚未消费。");
            context.EnemyDefense.Apply(enemies, targets, context);
            var allocation = new AttackAllocation(targets, parts.Sum(p => p.Value), Element.Physical, Components: parts);
            foreach (int target in targets) DuelActions.RecordUnitAttack(context, enemies[target], parts, phase);
            context.CombatPower.ConsumeAttacks(parts);
            context.CombatActionStarted = true;
            context.TurnActionCommitted = true;
            int beforeFame = player.Fame;
            EnterPhase(context, phase);
            int kills;
            if (phase == Phase.Ranged)
            {
                var result = RangedPhaseResolver.Resolve(enemies, new[] { allocation }, context);
                kills = result.KilledIndices.Count;
                ApplyRangedRewards(player, enemies, result, context);
            }
            else
            {
                // The attack phase follows damage assignment. Preserve the caller's
                // skip set, while preventing a second enemy attack in this operation.
                var saved = context.SkipAttackIndices.ToArray();
                var previousSkips = context.OriginalAttackSkips;
                context.OriginalAttackSkips = new HashSet<int>(saved);
                try
                {
                    for (int i = 0; i < enemies.Count; i++) context.SkipAttackIndices.Add(i);
                    var result = BattleResolver.Resolve(player, enemies, Array.Empty<BlockAllocation>(), new[] { allocation }, ctx: context);
                    kills = result.KilledIndices.Count;
                }
                finally
                {
                    context.OriginalAttackSkips = previousSkips;
                    context.SkipAttackIndices.Clear();
                    foreach (int i in saved) context.SkipAttackIndices.Add(i);
                }
            }
            return new Result(parts.Sum(p => p.Value), effective, required, kills, player.Fame - beforeFame, 0);
        }

        public static Result Block(PlayerState player, ActionContext context, Monster enemy)
            => Block(player, context, new[] { enemy }, 0);

        public static Result Block(PlayerState player, ActionContext context, IList<Monster> enemies, int target, int attackIndex = 0)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (context.TemporalPortalTurn) throw new InvalidOperationException("时光传送已占用本回合行动");
            if (context.PeacefulMomentAction) throw new InvalidOperationException("宁静时光已占用本回合行动");
            if (target < 0 || target >= enemies.Count) throw new InvalidOperationException("格挡目标无效");
            if (context.DefeatedEnemyIndices.Contains(target)) throw new InvalidOperationException("目标已经被击败");
            if (attackIndex < 0 || attackIndex >= enemies[target].AttackCount) throw new InvalidOperationException("敌人攻击序号无效");
            context.TurnActionCommitted = true;
            var enemy = DefenseProfile(enemies[target], target, context, attackIndex);
            EnterPhase(context, Phase.Block);
            var selected = context.CombatPower.AvailableBlocks(target, attackIndex, enemies[target]);
            var blocks = CombatMath.ResolveBlocks(enemy, selected)
                .Select(b => b with { TargetIndex = target, AttackIndex = attackIndex }).ToArray();
            int printed = blocks.Sum(b => b.Value);
            var ignored = context.IgnoredAbilities.TryGetValue(target, out var ability)
                ? new HashSet<Ability> { ability } : null;
            var element = context.AttackElementChange.GetValueOrDefault(target, enemy.AttackElement);
            int effective = CombatMath.EffectiveBlock(element, blocks,
                enemy.Abilities.Contains(Ability.Swift) && ignored?.Contains(Ability.Swift) != true);
            int required = AbilityRules.RequiredBlock(enemy, ignored);
            context.CombatPower.ConsumeBlocks(selected, target, attackIndex);
            context.CombatActionStarted = true;
            var saved = context.SkipAttackIndices.ToArray();
            try
            {
                for (int i = 0; i < enemies.Count; i++) if (i != target) context.SkipAttackIndices.Add(i);
                var result = BattleResolver.Resolve(player, enemies, blocks, Array.Empty<AttackAllocation>(), ctx: context,
                    enemyAttacks: new[] { (target, attackIndex) });
                return new Result(printed, effective, required, result.KilledIndices.Count, result.FameGain, result.TotalWounds);
            }
            finally
            {
                context.SkipAttackIndices.Clear();
                foreach (int i in saved) context.SkipAttackIndices.Add(i);
            }
        }

        /// <summary>非攻击效果直接消灭：不消费攻击贡献，不触发某笔攻击专属奖励。</summary>
        public static void DestroyEnemy(PlayerState player, ActionContext context, int target)
        {
            if (!context.InBattle || context.Enemies == null || target < 0 || target >= context.Enemies.Count)
                throw new InvalidOperationException("消灭必须选择本场战斗中的有效敌人");
            if (context.DefeatedEnemyIndices.Contains(target)) throw new InvalidOperationException("目标已经被击败");
            var enemy = context.Enemies[target];
            if (enemy.Abilities.Contains(Ability.MagicResist)) throw new InvalidOperationException("奥术免疫敌人不能被直接消灭");
            context.DefeatedEnemyIndices.Add(target);
            context.TurnActionCommitted = true;
            // The temporary brown defender is discarded after defense; this does not defeat its summoner.
            if (context.Summoning.IsSummoned(enemy)) return;
            PuppetMasterActions.RecordDefeat(player, context, enemy);
            context.RecordWorldDefeat(enemy);
            context.TurnActionCommitted = true;
            player.Fame += enemy.Fame + EnemyRewardRules.TakeBonus(context, enemy, EnemyDefeatSource.DirectEffect) + context.SwordFamePerKill + context.BowFamePerKill + ScoutsActions.TakeDefeatBonus(context, enemy) + DuelActions.TakeDefeatBonus(context, enemy);
            context.GrantKillCrystals(player, enemy, target: target);
        }

        internal static void ApplyRangedRewards(PlayerState player, IList<Monster> enemies,
            RangedResult result, ActionContext context)
        {
            player.Fame += result.FameGained;
            player.Reputation += result.ReputationGained;
            if (context == null) return;
            context.ExplosiveBoltArmorUses += result.ExplosiveBoltArmorUses;
            context.DefeatedEnemyIndices.UnionWith(result.KilledIndices);
            foreach (int i in result.KilledIndices)
            {
                PuppetMasterActions.RecordDefeat(player, context, enemies[i]);
                context.RecordWorldDefeat(enemies[i]);
                player.Fame += ScoutsActions.TakeDefeatBonus(context, enemies[i]) + DuelActions.TakeDefeatBonus(context, enemies[i]);
            }
            player.Fame += (context.SwordFamePerKill + context.BowFamePerKill) * result.KilledIndices.Count;
            foreach (int i in result.KilledIndices)
                context.GrantKillCrystals(player, enemies[i],
                    result.AttackCrystals != null && result.AttackCrystals.TryGetValue(i, out int count) ? count : 0, i);
            if (result.KilledIndices.Count > 0 && context.PendingFameOnRangedKill > 0)
            {
                player.Fame += context.PendingFameOnRangedKill;
                context.PendingFameOnRangedKill = 0;
            }
        }
    }
}
