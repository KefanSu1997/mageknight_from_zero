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
        public IReadOnlyList<AttackProfile> Attacks => _attacks.AsReadOnly();
        public IReadOnlyList<BlockAllocation> Blocks => _blocks.AsReadOnly();
        public int Total(AttackType type) => _attacks.Where(a => a.Type == type).Sum(a => a.Value);
        public int BlockTotal => _blocks.Sum(b => b.Value);

        public void AddAttack(int value, Element element = Element.Physical, AttackType type = AttackType.Melee)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > 0) _attacks.Add(new AttackProfile(type, value, element));
        }

        public void AddBlock(int value, Element element = Element.Physical, bool countEnemySymbols = false)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > 0) _blocks.Add(new BlockAllocation(0, value, element, CountEnemySymbols: countEnemySymbols));
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
                if (_attacks[i].Type == AttackType.Melee)
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

        public AttackProfile[] AvailableAttacks(Phase phase) => _attacks
            .Where(a => phase == Phase.Melee || (phase == Phase.Ranged && a.Type != AttackType.Melee)).ToArray();

        public void ConsumeAttacks(Phase phase)
            => _attacks.RemoveAll(a => phase == Phase.Melee || (phase == Phase.Ranged && a.Type != AttackType.Melee));

        public void ConsumeBlocks() => _blocks.Clear();
    }
}
