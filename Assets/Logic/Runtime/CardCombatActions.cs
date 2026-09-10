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
        public sealed record Result(int PrintedPower, int EffectivePower, int RequiredPower, int Kills, int Fame, int Wounds);

        public static Result Attack(PlayerState player, ActionContext context, IList<Monster> enemies,
            IReadOnlyList<int> targets, Phase phase)
        {
            if (phase != Phase.Ranged && phase != Phase.Melee) throw new InvalidOperationException("当前不是攻击阶段");
            if (targets == null || targets.Count == 0 || targets.Distinct().Count() != targets.Count ||
                targets.Any(i => i < 0 || i >= enemies.Count)) throw new InvalidOperationException("攻击目标无效");
            var parts = context.CombatPower.AvailableAttacks(phase);
            if (parts.Length == 0) throw new InvalidOperationException("没有可用于此阶段的攻击");
            if (!CombatMath.CanAttack(enemies, targets, parts, phase, context))
                throw new InvalidOperationException("城防限制：当前攻击不能指定这些目标");
            int required = targets.Sum(i => context.ArmorReduction.TryGetValue(i, out int reduction)
                ? Math.Max(1, enemies[i].Armor - reduction) : enemies[i].Armor);
            int effective = CombatMath.EffectiveAttack(enemies, targets, parts, phase, context);
            var allocation = new AttackAllocation(targets, parts.Sum(p => p.Value), Element.Physical, Components: parts);
            context.CombatPower.ConsumeAttacks(phase);
            int beforeFame = player.Fame;
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
                try
                {
                    for (int i = 0; i < enemies.Count; i++) context.SkipAttackIndices.Add(i);
                    var result = BattleResolver.Resolve(player, enemies, Array.Empty<BlockAllocation>(), new[] { allocation }, ctx: context);
                    kills = result.KilledIndices.Count;
                }
                finally
                {
                    context.SkipAttackIndices.Clear();
                    foreach (int i in saved) context.SkipAttackIndices.Add(i);
                }
            }
            return new Result(parts.Sum(p => p.Value), effective, required, kills, player.Fame - beforeFame, 0);
        }

        public static Result Block(PlayerState player, ActionContext context, Monster enemy)
        {
            var blocks = context.CombatPower.Blocks.ToArray();
            int printed = blocks.Sum(b => b.Value);
            int effective = CombatMath.EffectiveBlock(enemy.AttackElement, blocks);
            int required = AbilityRules.RequiredBlock(enemy);
            context.CombatPower.ConsumeBlocks();
            var result = BattleResolver.Resolve(player, new[] { enemy }, blocks, Array.Empty<AttackAllocation>(), ctx: context);
            return new Result(printed, effective, required, result.KilledIndices.Count, result.FameGain, result.TotalWounds);
        }

        internal static void ApplyRangedRewards(PlayerState player, IList<Monster> enemies,
            RangedResult result, ActionContext context)
        {
            player.Fame += result.FameGained;
            if (context == null) return;
            foreach (int i in result.KilledIndices)
                if (context.CrystalOnKill != null && context.CrystalsPerKill > 0)
                    player.Mana.AddCrystal(context.CrystalOnKill(enemies[i]), context.CrystalsPerKill);
            if (result.KilledIndices.Count > 0 && context.PendingFameOnRangedKill > 0)
            {
                player.Fame += context.PendingFameOnRangedKill;
                context.PendingFameOnRangedKill = 0;
            }
        }
    }
}
