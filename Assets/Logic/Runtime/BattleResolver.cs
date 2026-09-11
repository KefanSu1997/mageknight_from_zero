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
            IReadOnlyList<UnitDamageAllocation>? damageAssignments = null)
        {
            // 在分配伤害等状态变更前拒绝绕过卡牌操作入口的非法附身攻击。
            foreach (var attack in attacks ?? Array.Empty<AttackAllocation>())
            {
                var targets = attack.TargetIndices == null || attack.TargetIndices.Count == 0
                    ? Enumerable.Range(0, enemies.Count).ToArray() : attack.TargetIndices;
                if (CombatMath.Parts(attack).Any(p => p.ExcludedTargetIndex >= 0 && targets.Contains(p.ExcludedTargetIndex)))
                    throw new InvalidOperationException("附身攻击不能指定其来源敌人");
            }
            var damageContext = ctx ?? new ActionContext();
            var damagePlan = new Dictionary<int, IReadOnlyList<UnitState>>();
            if (damageAssignments != null)
            {
                if (blocks != null && blocks.Any(b => b.ForUnitIndex.HasValue))
                    throw new InvalidOperationException("承伤目标不能同时来自新旧两种分配");
                foreach (var assignment in damageAssignments)
                {
                    if (assignment == null || assignment.TargetIndex < 0 || assignment.TargetIndex >= enemies.Count
                        || assignment.Units == null || damagePlan.ContainsKey(assignment.TargetIndex))
                        throw new InvalidOperationException("承伤敌人或分配列表无效");
                    damagePlan.Add(assignment.TargetIndex, assignment.Units);
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
                    if (!damagePlan.ContainsKey(block.TargetIndex)) damagePlan.Add(block.TargetIndex, new[] { units[index] });
                }
            }
            var selectedUnits = new HashSet<UnitState>();
            foreach (var unit in damagePlan.Values.SelectMany(value => value))
            {
                UnitDamageResolver.Validate(player, damageContext, unit);
                if (!selectedUnits.Add(unit)) throw new InvalidOperationException("同场战斗不能重复向该部队分配伤害");
            }
            logger?.Log($"BattleStart {string.Join(',', enemies.Select(e => e.Id))}");
            logger?.Log("Phase Block");
            /*──────────────────────────────────────────────
             * 1️⃣ 预计算：每只怪物被格挡的数值
             *────────────────────────────────────────────*/
            var blockSum = new int[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                Element element = enemies[i].AttackElement;
                if (ctx != null && ctx.AttackElementChange.TryGetValue(i, out var changed)) element = changed;
                blockSum[i] = CombatMath.EffectiveBlock(element,
                    CombatMath.ResolveBlocks(enemies[i], (blocks ?? Array.Empty<BlockAllocation>()).Where(b => b.TargetIndex == i)));
            }
            for (int i = 0; i < blockSum.Length; i++)
                logger?.Log($"BlockSum {i} {blockSum[i]}");

            int woundsHero = 0;
            int blockedCount = 0;
            var killIndices = new List<int>();
            int fameGain   = 0;

            logger?.Log("Phase AssignDamage");
            /*──────────────────────────────────────────────
             * 2️⃣ Assign-Damage：剩余伤害 → 创伤
             *────────────────────────────────────────────*/
            for (int idx = 0; idx < enemies.Count; idx++)
            {
                var m = enemies[idx];
                if (ctx != null && ctx.SkipAttackIndices.Contains(idx))
                    continue;

                HashSet<Ability>? ignore = null;
                if (ctx != null && ctx.IgnoredAbilities.TryGetValue(idx, out var ab))
                    ignore = new HashSet<Ability> { ab };

                // ── 先計算是否完全格擋 ──
                int needBlock = AbilityRules.RequiredBlock(m, ignore);
                int val = blockSum[idx];
                if (ctx != null && m.Abilities.Contains(Ability.Swift) && (ignore == null || !ignore.Contains(Ability.Swift)))
                    val += ctx.SwiftBlockBonus;
                bool blocked  = val >= needBlock;
                logger?.Log($"BlockCheck {idx} need {needBlock} val {val} blocked {blocked}");
                if (blocked && ctx != null)
                {
                    blockedCount++;
                    if (ctx.FamePerUnitAction > 0)
                        player.Fame += ctx.FamePerUnitAction;
                    if (ctx.BlockedArmorOne)
                        ctx.ArmorReduction[idx] = m.Armor - 1;
                    foreach (var block in (blocks ?? Array.Empty<BlockAllocation>()).Where(b => b.TargetIndex == idx))
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
                    if (idx == 0)
                        atk = Math.Max(0, atk - attackReduction);
                    rawDamage = atk;
                }

                int dmgLeft = AbilityRules.BrutalizedDamage(m, rawDamage, ignore);
                logger?.Log($"AssignDamage {idx} dmg {dmgLeft}");

                if (dmgLeft == 0) continue;

                if (damagePlan.TryGetValue(idx, out var damageUnits))
                {
                    Element damageElement = m.AttackElement;
                    if (ctx != null && ctx.AttackElementChange.TryGetValue(idx, out var replacement)) damageElement = replacement;
                    foreach (var unit in damageUnits)
                    {
                        if (dmgLeft == 0) break;
                        dmgLeft = UnitDamageResolver.Assign(player, damageContext, unit, m, dmgLeft, damageElement, ignore);
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

                        if (elem == Element.Physical)
                        {
                            int valP = Math.Min(ctx.IgnorePhysicalDamage, dmgLeft);
                            ignoreAmt += valP;
                            ctx.IgnorePhysicalDamage -= valP;
                        }
                        else
                        {
                            int valN = Math.Min(ctx.IgnoreNonPhysicalDamage, dmgLeft);
                            ignoreAmt += valN;
                            ctx.IgnoreNonPhysicalDamage -= valN;
                        }

                        bool fireIce = elem == Element.Fire || elem == Element.Ice;
                        if (fireIce)
                        {
                            int valF = Math.Min(ctx.IgnoreFireIceDamage, dmgLeft - ignoreAmt);
                            ignoreAmt += valF;
                            ctx.IgnoreFireIceDamage -= valF;
                        }
                        else
                        {
                            int valO = Math.Min(ctx.IgnoreOtherDamage, dmgLeft - ignoreAmt);
                            ignoreAmt += valO;
                            ctx.IgnoreOtherDamage -= valO;
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
                    woundsHero += record;
                    logger?.Log($"HeroWound {record}");
                }
            }

            if (ctx != null && ctx.AttackBonusPerWound > 0 && woundsHero > 0)
            {
                int add = ctx.AttackBonusPerWound * woundsHero;
                if (ctx.AttackBonusLimit > 0)
                    add = Math.Min(add, ctx.AttackBonusLimit);
                ctx.MeleePool += add;
            }
            if (ctx != null && ctx.AttackBonusPerBlock > 0 && blockedCount > 0)
            {
                ctx.MeleePool += ctx.AttackBonusPerBlock * blockedCount;
            }

            /*──────────────────────────────────────────────
             * 3️⃣ Melee / 组攻 结算（与之前相同）
             *────────────────────────────────────────────*/
            logger?.Log("Phase Attack");
            var alive = Enumerable.Range(0, enemies.Count).ToHashSet(); // 稳定的原始索引
            logger?.Log($"Kills {string.Join(',', killIndices)}");
            foreach (int k in killIndices.OrderByDescending(x => x))
            {
                var m = enemies[k];
                fameGain += m.Fame;
                if (ctx != null && ctx.CrystalOnKill != null && ctx.CrystalsPerKill > 0)
                {
                    var color = ctx.CrystalOnKill(m);
                    player.Mana.AddCrystal(color, ctx.CrystalsPerKill);
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

                int armorSum = tgtIdx.Sum(i => ctx != null && ctx.ArmorReduction.TryGetValue(i, out int red)
                    ? Math.Max(1, enemies[i].Armor - red) : enemies[i].Armor);
                int effAtk = CombatMath.EffectiveAttack(enemies, tgtIdx, CombatMath.Parts(atk), Phase.Melee, ctx);

                if (effAtk >= armorSum)
                {
                    foreach (int i in tgtIdx.OrderByDescending(x => x))
                    {
                        var m = enemies[i];
                        fameGain += m.Fame;
                        if (ctx != null && ctx.CrystalOnKill != null && ctx.CrystalsPerKill > 0)
                        {
                            var color = ctx.CrystalOnKill(m);
                            player.Mana.AddCrystal(color, ctx.CrystalsPerKill);
                        }
                        alive.Remove(i);
                    }
                }
            }

            int killCount = enemies.Count - alive.Count;
            if (ctx != null)
            {
                if (ctx.PendingReputationOnKill > 0 && killCount > 0)
                    player.Reputation += ctx.PendingReputationOnKill * killCount;
                if (ctx.PendingFameOnKill > 0 && killCount > 0)
                    player.Fame += ctx.PendingFameOnKill * killCount;

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
            player.Fame += fameGain;
            var result = new BattleResult(alive.Count == 0, woundsHero, fameGain)
            { KilledIndices = Enumerable.Range(0, enemies.Count).Where(i => !alive.Contains(i)).ToArray() };
            logger?.Log($"BattleEnd {result.AllKilled} wounds {result.TotalWounds}");
            return result;
        }
    }
}
