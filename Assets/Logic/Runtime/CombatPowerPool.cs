using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>保留每一笔攻击的方式和元素，不能把攻城或元素攻击折成普通攻击。</summary>
    public sealed class CombatPowerPool
    {
        private readonly List<AttackProfile> _attacks = new();
        private readonly List<BlockAllocation> _blocks = new();
        internal object CurrentSourceCard { get; set; }
        private int _nextBlockGroup;
        private readonly Dictionary<int, HashSet<(int Target, int Attack)>> _usedBlockTargets = new();
        public IReadOnlyList<AttackProfile> Attacks => _attacks.AsReadOnly();
        public IReadOnlyList<BlockAllocation> Blocks => _blocks.AsReadOnly();
        public int Total(AttackType type) => _attacks.Where(a => a.Type == type).Sum(a => a.Value);
        public int BlockTotal => _blocks.Sum(b => b.Value);

        public void SetStarBowChoice(int index, int choice)
        {
            if (index < 0 || index >= _attacks.Count || choice < 0 || choice > 2
                || _attacks[index].Type == AttackType.Melee
                || _attacks[index].Type == AttackType.Siege && choice == 2)
                throw new InvalidOperationException("星辰之弓必须对一笔远程选择翻倍或攻城，或对攻城选择翻倍并变远程");
            _attacks[index] = _attacks[index] with { StarBowChoice = choice };
        }

        public void AddAttack(int value, Element element = Element.Physical, AttackType type = AttackType.Melee,
            int excludedTargetIndex = -1, int famePerDefeatedEnemy = 0, int reputationPerDefeatedEnemy = 0, int fameIfAnyDefeated = 0,
            Monster excludedEnemy = null, int crystalsPerDefeatedEnemy = 0, int armorReductionsPerDefeatedEnemy = 0, UnitState sourceUnit = null,
            Monster requiredEnemy = null, SkillCard sourceSkill = null, bool fromUnitAbility = false)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > 0) _attacks.Add(new AttackProfile(type, value, element, ExcludedTargetIndex: excludedTargetIndex,
                FamePerDefeatedEnemy: famePerDefeatedEnemy, ReputationPerDefeatedEnemy: reputationPerDefeatedEnemy, FameIfAnyDefeated: fameIfAnyDefeated,
                ExcludedEnemy: excludedEnemy, CrystalsPerDefeatedEnemy: crystalsPerDefeatedEnemy,
                ArmorReductionsPerDefeatedEnemy: armorReductionsPerDefeatedEnemy, SourceUnit: sourceUnit,
                SourceCard: sourceUnit == null && sourceSkill == null ? CurrentSourceCard : null,
                RequiredEnemy: requiredEnemy, SourceSkill: sourceSkill, FromUnitAbility: fromUnitAbility || sourceUnit != null));
        }

        public void AddBlock(int value, Element element = Element.Physical, bool countEnemySymbols = false,
            int armorReductionOnSuccess = 0, int attackOnSuccess = 0, bool killOnSuccess = false,
            bool doubleAgainstSwift = false, bool reduceArmorByExcess = false,
            Monster requiredEnemy = null, UnitState sourceUnit = null, SkillCard sourceSkill = null,
            bool fromUnitAbility = false, int requiredAttackIndex = -1)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > 0 || countEnemySymbols) _blocks.Add(new BlockAllocation(0, value, element, CountEnemySymbols: countEnemySymbols,
                ArmorReductionOnSuccess: armorReductionOnSuccess, AttackOnSuccess: attackOnSuccess, KillOnSuccess: killOnSuccess,
                DoubleAgainstSwift: doubleAgainstSwift, ReduceArmorByExcess: reduceArmorByExcess,
                RequiredEnemy: requiredEnemy, SourceUnit: sourceUnit, SourceSkill: sourceSkill,
                FromUnitAbility: fromUnitAbility || sourceUnit != null, RequiredAttackIndex: requiredAttackIndex,
                SourceCard: sourceUnit == null && sourceSkill == null && !fromUnitAbility ? CurrentSourceCard : null));
        }

        // Compatibility for physical effects. Reductions remove recent contributions
        // without changing the elements of the remaining power.
        public void SetTotal(AttackType type, int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            int difference = value - Total(type);
            if (difference >= 0) { AddAttack(difference, type: type); return; }
            for (int i = _attacks.Count - 1; i >= 0 && difference < 0; i--)
            {
                var part = _attacks[i];
                if (part.Type != type) continue;
                int remove = Math.Min(part.Value, -difference);
                difference += remove;
                if (remove == part.Value) _attacks.RemoveAt(i);
                else _attacks[i] = part with { Value = part.Value - remove };
            }
        }

        public void SetBlockTotal(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            int difference = value - BlockTotal;
            if (difference >= 0) { AddBlock(difference); return; }
            for (int i = _blocks.Count - 1; i >= 0 && difference < 0; i--)
            {
                int remove = Math.Min(_blocks[i].Value, -difference);
                difference += remove;
                if (remove == _blocks[i].Value) _blocks.RemoveAt(i);
                else _blocks[i] = _blocks[i] with { Value = _blocks[i].Value - remove };
            }
        }

        public void ConvertNewMeleeToRanged(int firstContribution)
        {
            for (int i = firstContribution; i < _attacks.Count; i++)
                if (_attacks[i].Type == AttackType.Melee && !_attacks[i].HasExcludedTarget)
                    _attacks[i] = _attacks[i] with { Type = AttackType.Ranged };
        }

        public void BoostAttack(AttackType type, int value)
        {
            int index = _attacks.FindLastIndex(a => a.Type == type);
            if (index < 0 || value < 0) throw new InvalidOperationException("没有可加值的攻击贡献");
            _attacks[index] = _attacks[index] with { Value = _attacks[index].Value + value };
        }

        public void BoostLastAttack(int value)
        {
            if (_attacks.Count == 0) throw new InvalidOperationException("没有可加值的攻击贡献");
            BoostAttack(_attacks[_attacks.Count - 1].Type, value);
        }

        public void BoostBlock(int value)
        {
            if (_blocks.Count == 0 || value < 0) throw new InvalidOperationException("没有可加值的格挡贡献");
            int index = _blocks.Count - 1;
            _blocks[index] = _blocks[index] with { Value = _blocks[index].Value + value };
        }

        public void BoostExactAttack(AttackProfile contribution, int value)
        {
            int index = _attacks.FindIndex(a => ReferenceEquals(a, contribution));
            if (index < 0 || value <= 0) throw new InvalidOperationException("请选择尚未消费的实际攻击贡献");
            _attacks[index] = contribution with { Value = contribution.Value + value };
        }

        public void BoostExactBlock(BlockAllocation contribution, int value)
        {
            int index = _blocks.FindIndex(b => ReferenceEquals(b, contribution));
            if (index < 0 || value <= 0) throw new InvalidOperationException("请选择尚未消费的实际格挡贡献");
            _blocks[index] = contribution with { Value = contribution.Value + value };
        }

        public AttackProfile[] AvailableAttacks(Phase phase, bool allSiege = false) => _attacks
            .Where(a => phase == Phase.Melee || (phase == Phase.Ranged && (allSiege || a.Type != AttackType.Melee))).ToArray();

        public AttackProfile[] AvailableAttacks(Phase phase, IReadOnlyList<int> targets, IList<Monster> enemies, bool allSiege = false) => AvailableAttacks(phase, allSiege)
            .Where(a => (!a.HasExcludedTarget || (phase == Phase.Melee && !targets.Any(i => a.Excludes(enemies[i], i))))
                && (a.RequiredEnemy == null || targets.Any(i => ReferenceEquals(a.RequiredEnemy, enemies[i])))).ToArray();

        public void ConsumeAttacks(IReadOnlyList<AttackProfile> selected)
        {
            foreach (var part in selected)
            {
                int index = _attacks.FindIndex(a => ReferenceEquals(a, part));
                if (index < 0) throw new InvalidOperationException("攻击贡献已被消耗");
                _attacks.RemoveAt(index);
            }
        }

        public void ClearPossessedAttacks() => _attacks.RemoveAll(a => a.HasExcludedTarget);

        public void ConsumeAttacks(Phase phase)
            => _attacks.RemoveAll(a => phase == Phase.Melee || (phase == Phase.Ranged && a.Type != AttackType.Melee));

        public void AddDistinctBlocks(int value, Element element, int count)
        {
            if (value <= 0 || count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            int group = ++_nextBlockGroup;
            _usedBlockTargets[group] = new HashSet<(int, int)>();
            for (int i = 0; i < count; i++)
                _blocks.Add(new BlockAllocation(0, value, element, DistinctAttackGroup: group));
        }

        // Retain one contribution from each split card per actual enemy attack.
        // A different attack of the same enemy may consume the next contribution.
        public BlockAllocation[] AvailableBlocks(int target, int attack = 0, Monster enemy = null)
        {
            var eligible = _blocks.Where(b => (b.RequiredEnemy == null || ReferenceEquals(b.RequiredEnemy, enemy)) && (b.RequiredAttackIndex < 0 || b.RequiredAttackIndex == attack)).ToArray();
            var selected = eligible.Where(b => b.DistinctAttackGroup == 0).ToList();
            foreach (var group in eligible.Where(b => b.DistinctAttackGroup != 0).GroupBy(b => b.DistinctAttackGroup))
                if (!_usedBlockTargets[group.Key].Contains((target, attack))) selected.Add(group.First());
            if (selected.Count == 0 && eligible.Length > 0)
                throw new InvalidOperationException("分次格挡必须选择尚未使用该效果的攻击");
            return selected.ToArray();
        }

        public void ConsumeBlocks(IReadOnlyList<BlockAllocation> selected, int target, int attack = 0)
        {
            foreach (var block in selected)
            {
                int index = _blocks.FindIndex(b => ReferenceEquals(b, block));
                if (index < 0) throw new InvalidOperationException("格挡贡献已被消耗");
                _blocks.RemoveAt(index);
                if (block.DistinctAttackGroup != 0) _usedBlockTargets[block.DistinctAttackGroup].Add((target, attack));
            }
        }

        public void ConsumeBlocks() { _blocks.Clear(); _usedBlockTargets.Clear(); }
    }
}
