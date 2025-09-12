namespace MK.Logic.Data
{
    using MK.Logic.Core;

    /// <summary>
    /// 描述一张可招募部队卡的静态属性，通常由解析卡牌图像脚本生成。
    /// </summary>
    public sealed record AttackProfile(
        AttackType Type,            // 攻击所属阶段：近战 / 远程 / 攻城
        int        Value,           // 攻击力数值
        Element    Element,         // 元素类型
        bool       IsSweep = false  // 是否带有“范围”效果
    );

    public sealed record UnitCard
    (
        string Id,                 // 唯一标识符
        string NameCn,
        string NameEn,
        int    Level,              // 等级决定承受伤口上限
        int    Armor,              // 护甲值
        int    InfluenceCost,      // 基础招募花费
        RecruitLocation RecruitLocationMask,  // 可招募地点掩码
        AttackProfile[] Attacks,   // 能造成的攻击列表
        Ability[] Abilities,       // 其他特殊能力
        bool IsElite = false       // 是否为精英单位，难民营招募时会额外加费
    );
}
