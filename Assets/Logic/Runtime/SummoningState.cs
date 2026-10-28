using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed record SummonBinding(int Index, Monster Summoner, Monster Token, int OriginalIndex);

    /// <summary>Rulebook p24: own actual brown tokens only during block and damage, then return them.</summary>
    public sealed class SummoningState
    {
        private readonly List<SummonBinding> bindings = new();
        private EnemyTokenSupply supply;
        private ActionContext savedModifiers;
        private Monster[] savedEnemies;
        private readonly Dictionary<int, int> survivingPositions = new();
        public IReadOnlyList<SummonBinding> Bindings => bindings.AsReadOnly();
        public bool IsActive { get; private set; }

        public void BeginDefense(IList<Monster> enemies, EnemyTokenSupply enemySupply,
            ISet<int> defeated, ISet<int> skipped)
        {
            if (IsActive) throw new InvalidOperationException("本场召唤攻击已经抽取，不能重复抽取");
            if (enemies == null || defeated == null || skipped == null) throw new ArgumentNullException();
            var originals = Enumerable.Range(0, enemies.Count)
                .Where(i => enemies[i].SummonAttack && !defeated.Contains(i) && !skipped.Contains(i)).ToArray();
            if (originals.Any(i => enemies[i].SummonCount < 1 || enemies[i].SummonCount > 2))
                throw new InvalidOperationException("原召唤图标数量无效");
            int required = originals.Sum(i => enemies[i].SummonCount);
            if (originals.Length > 0 && (enemySupply == null || enemySupply.DrawPile.Concat(enemySupply.DiscardPile)
                .Count(t => t.TokenColor == EnemyTokenColor.Brown) < required))
                throw new InvalidOperationException("召唤攻击缺少足够实际棕色标记；不能以零点攻击或模板代替");
            supply = enemySupply;
            int defenseIndex = 0;
            for (int index = 0; index < enemies.Count; index++)
            {
                if (!originals.Contains(index)) { survivingPositions.Add(index, defenseIndex++); continue; }
                for (int slot = 0; slot < enemies[index].SummonCount; slot++)
                    bindings.Add(new SummonBinding(defenseIndex++, enemies[index], supply.DrawRandom(EnemyTokenColor.Brown), index));
            }
            IsActive = true;
        }

        public Monster DefenderAt(int index, IList<Monster> originals)
            => bindings.FirstOrDefault(b => b.OriginalIndex == index)?.Token ?? originals[index];
        public bool IsSummoned(Monster token) => bindings.Any(b => ReferenceEquals(b.Token, token));

        public void ReplaceDefenders(ActionContext context)
        {
            if (context.Enemies.IsReadOnly)
                throw new InvalidOperationException("正式召唤替换需要可变的实际敌人列表");
            var skipped = new HashSet<int>(context.SkipAttackIndices);
            for (int index = 0; index < context.Enemies.Count; index++)
                if (context.ColdStareSkips(context.Enemies[index])) skipped.Add(index);
            BeginDefense(context.Enemies, context.EnemySupply, context.DefeatedEnemyIndices, skipped);
            savedEnemies = context.Enemies.ToArray();
            savedModifiers = context.ExtractEnemyModifiers(bindings.Select(b => b.OriginalIndex).ToHashSet());
            context.RemapEnemyModifiers(survivingPositions);
            var defenders = new Monster[survivingPositions.Count + bindings.Count];
            foreach (var pair in survivingPositions) defenders[pair.Value] = savedEnemies[pair.Key];
            foreach (var binding in bindings) defenders[binding.Index] = binding.Token;
            context.Enemies.Clear();
            foreach (var enemy in defenders) context.Enemies.Add(enemy);
        }

        public void RestoreSummoners(ActionContext context)
        {
            if (!IsActive || savedModifiers == null) throw new InvalidOperationException("没有实际替换的召唤防御阶段");
            foreach (var binding in bindings)
                if (!ReferenceEquals(context.Enemies[binding.Index], binding.Token))
                    throw new InvalidOperationException("召唤标记的实际战斗位置已被替换，不能静默归还");
            foreach (var pair in survivingPositions)
                if (!ReferenceEquals(context.Enemies[pair.Value], savedEnemies[pair.Key]))
                    throw new InvalidOperationException("非召唤敌人的实际防御位置已改变");
            context.RemapEnemyModifiers(survivingPositions.ToDictionary(p => p.Value, p => p.Key));
            context.RestoreEnemyModifiers(savedModifiers, bindings.Select(b => b.OriginalIndex).ToHashSet());
            context.Enemies.Clear();
            foreach (var enemy in savedEnemies) context.Enemies.Add(enemy);
            EndDefense();
            savedModifiers = null;
            savedEnemies = null;
        }

        public void EndDefense()
        {
            if (!IsActive) throw new InvalidOperationException("召唤防御阶段尚未开始或已经归还");
            foreach (var binding in bindings) supply.Discard(binding.Token);
            bindings.Clear();
            survivingPositions.Clear();
            supply = null;
            IsActive = false;
        }
    }
}
