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
            IGameLogger?                   logger = null)
        {
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

                // 尝试查找一个“由单位承担”的格挡记录
                int? forUnit = null;
                if (blocks != null)
                {
                    foreach (var b in blocks)
                        if (b.TargetIndex == idx && b.ForUnitIndex.HasValue)
                        { forUnit = b.ForUnitIndex; break; }
                }

                bool allowUnit = ctx == null || !ctx.NoUnitDamage;
                bool dealtToUnit = allowUnit && units != null && forUnit.HasValue
                                   && forUnit.Value >= 0 && forUnit.Value < units.Count;

                if (dealtToUnit)
                {
                    /*—— 单位受创 ——*/
                    var u = units![forUnit!.Value];
                    int armor = u.Card.Armor + (ctx?.UnitArmorBonus ?? 0);
                    int w = (int)System.Math.Ceiling(
                        dmgLeft / (double)System.Math.Max(1, armor));

                    AbilityRules.AssignWounds(m, u, ref w, ignore);
                }
                else
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
