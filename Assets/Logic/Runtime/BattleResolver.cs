// ────────────────────────────────────────────────
// Runtime/BattleResolver.cs
// ------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 由於 C#9 尚未支援 <c>record struct</c>，因此改以普通結構體實作戰鬥結果。
    /// </summary>
    public readonly struct BattleResult
    {
        /// <summary>是否擊殺所有敵人。</summary>
        public bool AllKilled { get; init; }

        /// <summary>玩家承受的總創傷。</summary>
        public int TotalWounds { get; init; }

        /// <summary>本次戰鬥獲得的名望。</summary>
        public int FameGain { get; init; }

        /// <summary>正式结算器实际击杀的原始目标索引。</summary>
        public IReadOnlyList<int> KilledIndices { get; init; }

        /// <summary>建構函式：初始化三項統計值。</summary>
        public BattleResult(bool allKilled, int totalWounds, int fameGain)
        {
            AllKilled   = allKilled;
            TotalWounds = totalWounds;
            FameGain    = fameGain;
            KilledIndices = Array.Empty<int>();
        }
    }

    public static class BattleResolver
    {
        public static BattleResult Resolve(
            PlayerState                     player,
            IList<Monster>                  enemies,
            IReadOnlyList<BlockAllocation>  blocks,
            IReadOnlyList<AttackAllocation> attacks,
            IList<UnitState>?               units = null,
            int attackReduction             = 0,
            ActionContext?                 ctx = null,
            IGameLogger?                   logger = null,
            IReadOnlyList<UnitDamageAllocation>? damageAssignments = null,
            IReadOnlyCollection<(int Target, int Attack)>? enemyAttacks = null)
        {
            ctx ??= new ActionContext();
            var previouslyDefeated = ctx?.DefeatedEnemyIndices ?? new HashSet<int>();
            // 在分配伤害等状态变更前拒绝绕过卡牌操作入口的非法附身攻击。
            foreach (var attack in attacks ?? Array.Empty<AttackAllocation>())
            {
                var targets = attack.TargetIndices == null || attack.TargetIndices.Count == 0
                    ? Enumerable.Range(0, enemies.Count).Where(i => !previouslyDefeated.Contains(i)).ToArray() : attack.TargetIndices;
                if (targets.Any(previouslyDefeated.Contains)) throw new InvalidOperationException("目标已经被击败");
                if (CombatMath.Parts(attack).Any(p => p.HasExcludedTarget && targets.Any(i => i >= 0 && i < enemies.Count && p.Excludes(enemies[i], i))))
                    throw new InvalidOperationException("附身攻击不能指定其来源敌人");
            }
            foreach (var attack in attacks ?? Array.Empty<AttackAllocation>())
            {
                var targets = attack.TargetIndices == null || attack.TargetIndices.Count == 0
                    ? Enumerable.Range(0, enemies.Count).Where(i => !previouslyDefeated.Contains(i)).ToArray() : attack.TargetIndices;
                if (!CombatMath.CanAttack(enemies, targets, CombatMath.Parts(attack), Phase.Melee, ctx))
                    throw new InvalidOperationException("攻击贡献限定目标与实际选择不一致");
            }
            var damageContext = ctx ?? new ActionContext();
            damageContext.EnsureBattleWounds(player);
            var allAttacks = Enumerable.Range(0, enemies.Count)
                .SelectMany(i => Enumerable.Range(0, enemies[i].AttackCount).Select(a => (Target: i, Attack: a))).ToArray();
            if (enemyAttacks != null && enemyAttacks.Any(a => !allAttacks.Contains(a)))
                throw new InvalidOperationException("敌人攻击选择无效");
            var selectedAttacks = enemyAttacks == null ? allAttacks : allAttacks.Where(enemyAttacks.Contains).ToArray();
            foreach (var block in blocks ?? Array.Empty<BlockAllocation>())
                if (!allAttacks.Contains((block.TargetIndex, block.AttackIndex))
                    || block.RequiredEnemy != null && !ReferenceEquals(block.RequiredEnemy, enemies[block.TargetIndex])
                    || block.RequiredAttackIndex >= 0 && block.RequiredAttackIndex != block.AttackIndex)
                    throw new InvalidOperationException("格挡敌人或攻击序号无效");
            var damagePlan = new Dictionary<(int Target, int Attack), IReadOnlyList<UnitState>>();
            if (damageAssignments != null)
            {
                if (blocks != null && blocks.Any(b => b.ForUnitIndex.HasValue))
                    throw new InvalidOperationException("承伤目标不能同时来自新旧两种分配");
                foreach (var assignment in damageAssignments)
                {
                    if (assignment == null || assignment.TargetIndex < 0 || assignment.TargetIndex >= enemies.Count
                        || assignment.AttackIndex < 0 || assignment.AttackIndex >= enemies[assignment.TargetIndex].AttackCount
                        || assignment.Units == null || damagePlan.ContainsKey((assignment.TargetIndex, assignment.AttackIndex)))
                        throw new InvalidOperationException("承伤敌人或分配列表无效");
                    damagePlan.Add((assignment.TargetIndex, assignment.AttackIndex), assignment.Units);
                }
            }
            else if (blocks != null)
            {
                // 兼容旧调用，但不再把整次攻击吞进一支部队。
                foreach (var block in blocks.Where(b => b.ForUnitIndex.HasValue))
                {
                    int index = block.ForUnitIndex.Value;
                    if (units == null || index < 0 || index >= units.Count || block.TargetIndex < 0 || block.TargetIndex >= enemies.Count)
                        throw new InvalidOperationException("承伤部队索引无效");
                    if (!damagePlan.ContainsKey((block.TargetIndex, block.AttackIndex))) damagePlan.Add((block.TargetIndex, block.AttackIndex), new[] { units[index] });
                }
            }
            logger?.Log($"BattleStart {string.Join(',', enemies.Select(e => e.Id))}");
            logger?.Log("Phase Block");
            /*──────────────────────────────────────────────
             * 1️⃣ 预计算：每只怪物被格挡的数值
             *────────────────────────────────────────────*/
            var blockSum = new Dictionary<(int Target, int Attack), int>();
            foreach (var attackTarget in allAttacks)
            {
                int i = attackTarget.Target;
                var profile = CardCombatActions.DefenseProfile(enemies[i], i, ctx, attackTarget.Attack);
                Element element = profile.AttackElement;
                if (ctx != null && ctx.AttackElementChange.TryGetValue(i, out var changed)) element = changed;
                bool swift = profile.Abilities.Contains(Ability.Swift)
                    && (ctx == null || !ctx.IgnoredAbilities.TryGetValue(i, out var ignoredAbility) || ignoredAbility != Ability.Swift);
                blockSum[attackTarget] = CombatMath.EffectiveBlock(element,
                    CombatMath.ResolveBlocks(profile, (blocks ?? Array.Empty<BlockAllocation>()).Where(b => b.TargetIndex == i && b.AttackIndex == attackTarget.Attack)), swift);
            }
            foreach (var block in blockSum)
                logger?.Log($"BlockSum {block.Key} {block.Value}");

            // Original Shocktroops bind a real attack. Omitting a UI plan cannot bypass forced first damage.
            foreach (var key in selectedAttacks)
            {
                var forced = damageContext.ForcedDamageUnit(enemies[key.Target], key.Attack);
                if (forced == null || previouslyDefeated.Contains(key.Target)
                    || damageContext.IsAttackCancelled(key.Target, key.Attack) || damageContext.ColdStareSkips(enemies[key.Target])) continue;
                var profile = CardCombatActions.DefenseProfile(enemies[key.Target], key.Target, ctx, key.Attack);
                var ignored = damageContext.IgnoredAbilities.TryGetValue(key.Target, out var ignoredAbility)
                    ? new HashSet<Ability> { ignoredAbility } : null;
                if (profile.Attack <= 0 || blockSum[key] >= AbilityRules.RequiredBlock(profile, ignored)) continue;
                var rest = damagePlan.TryGetValue(key, out var planned) ? planned : Array.Empty<UnitState>();
                damagePlan[key] = new[] { forced }.Concat(rest.Where(u => !ReferenceEquals(u, forced))).ToArray();
            }
            var selectedUnits = new HashSet<UnitState>();
            foreach (var plan in damagePlan)
                foreach (var unit in plan.Value)
                {
                    UnitDamageResolver.Validate(player, damageContext, unit);
                    ShockTroopsActions.ValidateDamageUnit(damageContext, enemies[plan.Key.Target], plan.Key.Target, plan.Key.Attack, unit);
                    if (!selectedUnits.Add(unit)) throw new InvalidOperationException("同场战斗不能重复向该部队分配伤害");
                }

            // Validate the whole selected damage plan before any wound, payment or block reward.
            int influenceNeeded = 0;
            foreach (var plan in damagePlan)
            {
                int index = plan.Key.Target;
                if (!selectedAttacks.Contains(plan.Key) || previouslyDefeated.Contains(index)
                    || damageContext.IsAttackCancelled(index, plan.Key.Attack) || damageContext.ColdStareSkips(enemies[index])) continue;
                var defender = CardCombatActions.DefenseProfile(enemies[index], index, ctx, plan.Key.Attack);
                var ignored = damageContext.IgnoredAbilities.TryGetValue(index,out var ability) ? new HashSet<Ability>{ability} : null;
                int value = blockSum[plan.Key];
                if (value >= AbilityRules.RequiredBlock(defender,ignored)) continue;
                int damage = AbilityRules.BrutalizedDamage(defender,Math.Max(0,defender.Attack + damageContext.EnemyAttackAdjust.GetValueOrDefault(index) - (index==0 && plan.Key.Attack==0?attackReduction:0)),ignored);
                var element = damageContext.AttackElementChange.TryGetValue(index,out var changed) ? changed : defender.AttackElement;
                foreach (var unit in plan.Value)
                {
                    if (damage<=0) break;
                    influenceNeeded += unit.Card.DamageInfluenceCost;
                    damage = UnitDamageResolver.RemainingAfterAbsorption(damageContext,unit,damage,element);
                }
            }
            if (damageContext.InfluencePool < influenceNeeded)
                throw new InvalidOperationException("向此部队分配伤害所需影响力不足");

            damageContext.CombatActionStarted = true;
            int woundsHero = 0;
            int blockedCount = 0;
            var killIndices = new List<int>();
            int fameGain   = 0;

            logger?.Log("Phase AssignDamage");
            /*──────────────────────────────────────────────
             * 2️⃣ Assign-Damage：剩余伤害 → 创伤
             *────────────────────────────────────────────*/
            foreach (var attackTarget in selectedAttacks)
            {
                int idx = attackTarget.Target;
                var m = CardCombatActions.DefenseProfile(enemies[idx], idx, ctx, attackTarget.Attack);
                if (killIndices.Contains(idx) || previouslyDefeated.Contains(idx)
                    || ctx != null && (ctx.IsAttackCancelled(idx, attackTarget.Attack) || ctx.ColdStareSkips(enemies[idx])))
                    continue;

                if (ctx != null && (blocks ?? Array.Empty<BlockAllocation>()).Any(b => b.TargetIndex == idx
                    && b.AttackIndex == attackTarget.Attack && b.Value > 0 && (b.SourceUnit != null || b.FromUnitAbility)))
                    DuelActions.RecordUnitEffect(ctx, enemies[idx]);
                ctx?.PendingAttackReductions.Remove(attackTarget);
                ctx?.ConsumeColdStareAttack(enemies[idx], attackTarget.Attack);

                HashSet<Ability>? ignore = null;
                if (ctx != null && ctx.IgnoredAbilities.TryGetValue(idx, out var ab))
                    ignore = new HashSet<Ability> { ab };

                // ── 先計算是否完全格擋 ──
                int needBlock = AbilityRules.RequiredBlock(m, ignore);
                int val = blockSum[attackTarget];
                bool blocked  = val >= needBlock;
                logger?.Log($"BlockCheck {idx} need {needBlock} val {val} blocked {blocked}");
                if (blocked && ctx != null)
                {
                    // Zero attack during Block is a successful block; cancelled attacks never reach here.
                    ctx.SuccessfullyBlockedAttacks.Add((idx, attackTarget.Attack));
                    blockedCount++;
                    if (ctx.FamePerUnitAction > 0)
                        player.Fame += ctx.FamePerUnitAction;
                    if ((blocks ?? Array.Empty<BlockAllocation>()).Any(b => b.TargetIndex == idx && b.AttackIndex == attackTarget.Attack && b.Value > 0))
                    {
                        ctx.RecordBlockedEnemy(enemies[idx]);
                        ctx.SuccessfullyBlockedAttacks.Add((idx, attackTarget.Attack));
                    }
                    foreach (var block in (blocks ?? Array.Empty<BlockAllocation>()).Where(b => b.TargetIndex == idx && b.AttackIndex == attackTarget.Attack))
                    {
                        // A reward for the player is independent of enemy immunity;
                        // effects that directly alter this enemy respect its immunities.
                        if (block.AttackOnSuccess > 0)
                            ctx.CombatPower.AddAttack(block.AttackOnSuccess, block.Element);
                        bool immune = m.Abilities.Contains(Ability.MagicResist)
                            || (block.Element == Element.Fire && m.Abilities.Contains(Ability.FireResist))
                            || (block.Element == Element.Ice && m.Abilities.Contains(Ability.IceResist));
                        if (immune) continue;
                        if (block.KillOnSuccess && !killIndices.Contains(idx)) killIndices.Add(idx);
                        if (block.ArmorReductionOnSuccess > 0)
                            ctx.ArmorReduction[idx] = ctx.ArmorReduction.GetValueOrDefault(idx) + block.ArmorReductionOnSuccess;
                        // Shield Bash remains physical block, but its blue non-attack effect
                        // cannot reduce an ice-resistant enemy's armor (rulebook p24).
                        if (block.ReduceArmorByExcess && !m.Abilities.Contains(Ability.IceResist))
                            ctx.ArmorReduction[idx] = ctx.ArmorReduction.GetValueOrDefault(idx) + Math.Max(0, val - needBlock);
                    }
                }

                int rawDamage;
                if (blocked)
                {
                    // 完全格擋後不承受任何傷害
                    rawDamage = 0;
                }
                else
                {
                    // 部分格擋視同未格擋，直接吃完整攻擊值
                    int atk = m.Attack;
                    if (ctx != null && ctx.EnemyAttackAdjust.TryGetValue(idx, out int adj))
                        atk += adj;
                    if (idx == 0 && attackTarget.Attack == 0)
                        atk = Math.Max(0, atk - attackReduction);
                    rawDamage = atk;
                }

                int dmgLeft = AbilityRules.BrutalizedDamage(m, rawDamage, ignore);
                logger?.Log($"AssignDamage {idx} dmg {dmgLeft}");

                if (dmgLeft == 0) continue;

                if (damagePlan.TryGetValue(attackTarget, out var damageUnits))
                {
                    Element damageElement = m.AttackElement;
                    if (ctx != null && ctx.AttackElementChange.TryGetValue(idx, out var replacement)) damageElement = replacement;
                    foreach (var unit in damageUnits)
                    {
                        if (dmgLeft == 0) break;
                        dmgLeft = UnitDamageResolver.Assign(player, damageContext, unit, m, dmgLeft, damageElement, ignore,
                            assassinationException: ReferenceEquals(damageContext.ForcedDamageUnit(enemies[idx], attackTarget.Attack), unit), actualEnemy: enemies[idx]);
                    }
                }
                if (dmgLeft > 0)
                {
                    /*—— 英雄受创 ——*/
                    int ignoreAmt = 0;
                    if (ctx != null)
                    {
                        Element elem = m.AttackElement;
                        if (ctx.AttackElementChange.TryGetValue(idx, out var ne))
                            elem = ne;

                        if (ctx.HardenedMatches(enemies[idx], attackTarget.Attack) && elem == Element.Physical)
                        {
                            int valP = Math.Min(ctx.IgnorePhysicalDamage, dmgLeft);
                            ignoreAmt += valP;
                            ctx.IgnorePhysicalDamage = 0;
                            ctx.HardenedEnemy = null;
                        }
                        else if (ctx.HardenedMatches(enemies[idx], attackTarget.Attack))
                        {
                            int valN = Math.Min(ctx.IgnoreNonPhysicalDamage, dmgLeft);
                            ignoreAmt += valN;
                            ctx.IgnoreNonPhysicalDamage = 0;
                            ctx.HardenedEnemy = null;
                        }

                        bool fireIce = elem == Element.Fire || elem == Element.Ice;
                        if (fireIce)
                        {
                            int valF = Math.Min(ctx.IgnoreFireIceDamage, dmgLeft - ignoreAmt);
                            ignoreAmt += valF;
                            ctx.IgnoreFireIceDamage = 0;
                        }
                        else
                        {
                            int valO = Math.Min(ctx.IgnoreOtherDamage, dmgLeft - ignoreAmt);
                            ignoreAmt += valO;
                            ctx.IgnoreOtherDamage = 0;
                        }
                    }
                    dmgLeft -= ignoreAmt;
                    int w = (int)Math.Ceiling(dmgLeft / (double)Math.Max(1, player.Armor));

                    if (ctx != null && ctx.IgnoreNextWound && w > 0)
                    {
                        w -= 1;
                        ctx.IgnoreNextWound = false;
                    }

                    int record = w;
                    AbilityRules.AssignWounds(m, player, ref w, ignore);
                    if (record > 0 && m.Abilities.Contains(Ability.Vampirism)
                        && ignore?.Contains(Ability.Vampirism) != true)
                        damageContext.Vampirism.Add(enemies[idx], record);
                    damageContext.ResolveBattleWounds(player);
                    woundsHero += record;
                    logger?.Log($"HeroWound {record}");
                }
            }

            // Masters' red effect depends on this original enemy being fully blocked, not
            // on one of its attacks being cancelled or merely selected in the UI.
            if (ctx != null)
            {
                foreach (int index in Enumerable.Range(0, enemies.Count))
                {
                    var enemy = enemies[index];
                    if (previouslyDefeated.Contains(index) || killIndices.Contains(index)
                        || !ctx.MastersDestroyWhenBlocked.Any(e => ReferenceEquals(e, enemy))
                        || enemy.Abilities.Contains(Ability.MagicResist)
                        || CombatMath.HasResistance(enemy, index, Element.Fire, ctx)) continue;
                    bool allBlocked = Enumerable.Range(0, enemy.AttackCount).All(attack =>
                    {
                        if (ctx.IsAttackCancelled(index, attack) || ctx.ColdStareSkips(enemy)) return false;
                        if (ctx.SuccessfullyBlockedAttacks.Contains((index, attack))) return true;
                        if (!selectedAttacks.Contains((index, attack))) return false;
                        var profile = CardCombatActions.DefenseProfile(enemy, index, ctx, attack);
                        var ignored = ctx.IgnoredAbilities.TryGetValue(index, out var ignoredAbility)
                            ? new HashSet<Ability> { ignoredAbility } : null;
                        // An attack reduced to zero is successfully blocked without spending block.
                        return blockSum[(index, attack)] >= AbilityRules.RequiredBlock(profile, ignored);
                    });
                    if (allBlocked) killIndices.Add(index);
                }
            }

            if (ctx != null && ctx.AttackBonusPerBlock > 0 && blockedCount > 0)
            {
                ctx.MeleePool += ctx.AttackBonusPerBlock * blockedCount;
            }

            /*──────────────────────────────────────────────
             * 3️⃣ Melee / 组攻 结算（与之前相同）
             *────────────────────────────────────────────*/
            logger?.Log("Phase Attack");
            var alive = Enumerable.Range(0, enemies.Count).Where(i => !previouslyDefeated.Contains(i)).ToHashSet();
            var initiallyAlive = alive.ToArray();
            logger?.Log($"Kills {string.Join(',', killIndices)}");
            foreach (int k in killIndices.OrderByDescending(x => x))
            {
                var m = enemies[k];
                if (ctx?.Summoning.IsSummoned(m) != true)
                {
                    fameGain += m.Fame + EnemyRewardRules.TakeBonus(ctx, m, EnemyDefeatSource.BlockEffect);
                    ctx?.GrantKillCrystals(player, m, target: k);
                }
                alive.Remove(k);
            }
            foreach (var atk in attacks ?? Array.Empty<AttackAllocation>())
            {
                if (ctx != null && ctx.FamePerUnitAction > 0)
                    player.Fame += ctx.FamePerUnitAction;
                logger?.Log($"Attack {atk.Value} {atk.Element}");
                var tgtIdx = (atk.TargetIndices == null || atk.TargetIndices.Count == 0)
                             ? alive.OrderBy(i => i).ToList()
                             : atk.TargetIndices.Where(i => alive.Contains(i)).Distinct().ToList();
                if (tgtIdx.Count == 0) continue;

                ctx.EnemyDefense.Apply(enemies, tgtIdx, ctx, Enumerable.Range(0, enemies.Count).Where(i => !alive.Contains(i)));
                if (ctx != null)
                    foreach (int target in tgtIdx) DuelActions.RecordUnitAttack(ctx, enemies[target], CombatMath.Parts(atk), Phase.Melee);
                int armorSum = tgtIdx.Sum(i => CombatMath.EffectiveArmor(enemies[i], i, ctx, Phase.Melee));
                int effAtk = CombatMath.EffectiveAttack(enemies, tgtIdx, CombatMath.Parts(atk), Phase.Melee, ctx);

                if (effAtk >= armorSum)
                {
                    var reward = CombatMath.AttackRewards(CombatMath.Parts(atk), tgtIdx.Count);
                    fameGain += reward.Fame;
                    player.Reputation += reward.Reputation;
                    if (ctx != null)
                        ctx.ExplosiveBoltArmorUses += CombatMath.Parts(atk).Sum(p => p.ArmorReductionsPerDefeatedEnemy) * tgtIdx.Count;
                    foreach (int i in tgtIdx.OrderByDescending(x => x))
                    {
                        var m = enemies[i];
                        fameGain += m.Fame + EnemyRewardRules.TakeBonus(ctx, m, EnemyDefeatSource.Melee);
                        ctx?.GrantKillCrystals(player, m,
                            CombatMath.Parts(atk).Sum(p => p.CrystalsPerDefeatedEnemy), i);
                        alive.Remove(i);
                    }
                }
            }

            var newlyKilled = initiallyAlive.Where(i => !alive.Contains(i)).ToArray();
            var rewardedKills = newlyKilled.Where(i => ctx?.Summoning.IsSummoned(enemies[i]) != true).ToArray();
            int killCount = rewardedKills.Length;
            if (ctx != null)
            {
                if (ctx.PendingReputationOnKill > 0 && killCount > 0)
                    player.Reputation += ctx.PendingReputationOnKill * killCount;
                if (ctx.PendingFameOnKill > 0 && killCount > 0)
                    player.Fame += ctx.PendingFameOnKill * killCount;
                player.Fame += (ctx.SwordFamePerKill + ctx.BowFamePerKill) * killCount;

                if (ctx.DiscardWoundsAfterBattle > 0 && killCount > 0)
                {
                    int remove = System.Math.Min(killCount, ctx.DiscardWoundsAfterBattle);
                    foreach (var c in player.Deck.Hand.ToArray())
                    {
                        if (remove == 0) break;
                        if (c.Type == CardType.Wound)
                        {
                            player.Deck.Discard(c);
                            remove--;
                        }
                    }
                    ctx.DiscardWoundsAfterBattle = 0;
                }

                ctx.PendingReputationOnKill = 0;
                ctx.PendingFameOnKill = 0;
            }
            foreach (int i in rewardedKills) fameGain += ScoutsActions.TakeDefeatBonus(ctx, enemies[i]) + (ctx == null ? 0 : DuelActions.TakeDefeatBonus(ctx, enemies[i]));
            player.Fame += fameGain;
            ctx?.DefeatedEnemyIndices.UnionWith(newlyKilled);
            foreach (int i in rewardedKills)
            {
                if (ctx != null) PuppetMasterActions.RecordDefeat(player, ctx, enemies[i]);
                ctx?.RecordWorldDefeat(enemies[i]);
            }
            var result = new BattleResult(alive.Count == 0 && (ctx == null || ctx.Summoning.Bindings.Count == 0), woundsHero, fameGain)
            { KilledIndices = rewardedKills };
            logger?.Log($"BattleEnd {result.AllKilled} wounds {result.TotalWounds}");
            return result;
        }
    }
}
