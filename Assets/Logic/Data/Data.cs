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
        string MonsterType = "Unknown" // 額外：類型，用於特殊地點篩選
    );

    /// 针对单个敌人的格挡
    public sealed record BlockAllocation(
        int TargetIndex,
        int Value,
        Element Element,
        int? ForUnitIndex = null   // 新增：若非空表示此格挡由某个 Unit 承担
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


