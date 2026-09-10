using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Adventure
{
    /// <summary>稳定敌人索引下的阶段结算；格挡和攻击资源按目标与元素分别累计。</summary>
    public sealed class EncounterRound
    {
        private readonly Dictionary<(int target, Element element), int> _blocks = new();
        private readonly Dictionary<(int target, Element element), int> _attacks = new();
        public EncounterSpec Definition { get; }
        public bool DefenseResolved { get; private set; }
        public bool AttackResolved { get; private set; }
        public bool[] Defeated { get; }

        public EncounterRound(EncounterSpec definition)
        {
            if (definition?.Enemies == null || definition.Enemies.Length == 0)
                throw new ArgumentException("遭遇至少需要一名敌人。");
            Definition = definition;
            Defeated = new bool[definition.Enemies.Length];
        }

        public void Allocate(int target, CardAction action, int value, Element element)
        {
            if (target < 0 || target >= Defeated.Length || value < 0 || Defeated[target])
                throw new ArgumentOutOfRangeException(nameof(target));
            if (action == CardAction.Block && !DefenseResolved) Add(_blocks, target, element, value);
            else if (action == CardAction.Attack && DefenseResolved && !AttackResolved) Add(_attacks, target, element, value);
            else throw new InvalidOperationException("行动与战斗阶段不一致。");
        }

        private static void Add(Dictionary<(int, Element), int> pool, int target, Element element, int value)
            => pool[(target, element)] = pool.GetValueOrDefault((target, element)) + value;

        public int RequiredBlock(int target) => AbilityRules.RequiredBlock(Definition.Enemies[target].Monster());
        public int EffectiveBlock(int target) => CombatMath.EffectiveBlock(Definition.Enemies[target].Element,
            _blocks.Where(p => p.Key.target == target).Select(p => new BlockAllocation(target, p.Value, p.Key.element)));
        public int EffectiveAttack(int target) => CombatMath.EffectiveAttack(
            Definition.Enemies.Select(e => e.Monster()).ToArray(), new[] { target },
            _attacks.Where(p => p.Key.target == target).Select(p => new AttackProfile(AttackType.Melee, p.Value, p.Key.element)), Phase.Melee);

        public int ResolveDefense(PlayerState player)
        {
            if (DefenseResolved) throw new InvalidOperationException("格挡阶段已结算。");
            var blocks = _blocks.Select(p => new BlockAllocation(p.Key.target, p.Value, p.Key.element)).ToArray();
            var result = BattleResolver.Resolve(player, Definition.Enemies.Select(e => e.Monster()).ToArray(),
                blocks, Array.Empty<AttackAllocation>());
            DefenseResolved = true;
            return result.TotalWounds;
        }

        public int ResolveAttack(PlayerState player)
        {
            if (!DefenseResolved || AttackResolved) throw new InvalidOperationException("攻击阶段尚未开始或已经结算。");
            int fame = 0;
            for (int i = 0; i < Defeated.Length; i++)
            {
                // 元素资源已按目标合并并应用抗性；只将有效攻击交给结算器，防止再次折算。
                var context = new ActionContext { IgnoreResist = true };
                context.SkipAttackIndices.Add(0); // 此轮敌人伤害已在格挡阶段结算。
                var result = BattleResolver.Resolve(player, new[] { Definition.Enemies[i].Monster() },
                    Array.Empty<BlockAllocation>(), new[] { new AttackAllocation(new[] { 0 }, EffectiveAttack(i), Element.Physical) }, ctx: context);
                Defeated[i] = result.AllKilled;
                fame += result.FameGain;
            }
            AttackResolved = true;
            return fame;
        }
    }
}
