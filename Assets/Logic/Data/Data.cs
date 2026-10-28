// ────────────────────────────────────────────────
// Data/Monster.cs
// ------------------------------------------------
namespace MK.Logic.Data
{
    using System.Collections.Generic;
    using MK.Logic.Core;

    /// <summary>怪物静态数据 —— 由解析脚本自动生成</summary>
    public sealed /*不能被继承*/ record Monster(
        string Id,               // 唯一 ID，例如 "dragon_fire_01"
        int Armor,            // 顶部黑盾
        int Attack,           // 左侧数值
        Element AttackElement,   // 左侧底色
        int Fame,             // 下方红框
        IReadOnlyCollection<Ability> Abilities, // 右侧图标
        string MonsterType = "Unknown", // 額外：類型，用於特殊地點篩選
        EnemyTokenColor TokenColor = EnemyTokenColor.Unknown,
        IReadOnlyList<EnemyAttack> Attacks = null,
        int ElusiveArmor = 0,
        int DefendValue = 0,
        EnemyFaction Faction = EnemyFaction.None,
        bool SummonAttack = false,
        int SummonCount = 1
    )
    {
        public int AttackCount => SummonAttack ? 0 : Attacks?.Count ?? 1;

        public EnemyAttack GetAttack(int index)
        {
            if (index < 0 || index >= AttackCount) throw new System.ArgumentOutOfRangeException(nameof(index));
            // Keep the original scalar fields compatible with existing first-attack modifiers.
            return index == 0 ? new EnemyAttack(Attack, AttackElement) : Attacks[index];
        }
    }

    public sealed record EnemyAttack(int Value, Element Element);

    /// 针对单个敌人的格挡
    public sealed record BlockAllocation(
        int TargetIndex,
        int Value,
        Element Element,
        int? ForUnitIndex = null,  // 若非空表示此格挡由某个 Unit 承担
          bool CountEnemySymbols = false, // 寒冰护体：在实际目标结算时追加冰格挡
          int ArmorReductionOnSuccess = 0,
          int AttackOnSuccess = 0,
          bool KillOnSuccess = false,
          int DistinctAttackGroup = 0,
          bool DoubleAgainstSwift = false,
          bool ReduceArmorByExcess = false,
          int AttackIndex = 0,
          Monster RequiredEnemy = null,
          MK.Logic.Runtime.UnitState SourceUnit = null,
          SkillCard SourceSkill = null,
          bool FromUnitAbility = false,
          int RequiredAttackIndex = -1,
          object SourceCard = null
    );

        /// <summary>
    /// 一“列”攻击的数据：既可指定单个目标，也可指定多个目标(即“组攻”)。
    /// </summary>
    public sealed record AttackAllocation(
        IReadOnlyList<int>? TargetIndices, // null 或空 ⇒ 攻击全部剩余敌人
        int Value,                         // 攻击力
        Element Element,                   // 元素类型
        bool IsSiege = false,              // 旧调用的单一攻击方式
        IReadOnlyList<AttackProfile>? Components = null // 一次组攻可以组合多个元素和攻击方式
    );

}


