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
        bool       IsSweep = false, // 是否带有“范围”效果
        int ExcludedTargetIndex = -1, // 附身贡献不能攻击其来源敌人；仅在近战攻击阶段使用
        int StarBowChoice = 0, // 0默认：远程翻倍、攻城不变；1翻倍（攻城变远程）；2远程变攻城
        int FamePerDefeatedEnemy = 0,
        int ReputationPerDefeatedEnemy = 0,
        int FameIfAnyDefeated = 0,
        Monster ExcludedEnemy = null, // 正式附身绑定原敌人实例；列表压缩或重排不改变来源
        int CrystalsPerDefeatedEnemy = 0, // 灵魂收割者基础奖励仅属于消费此贡献的攻击
        int ArmorReductionsPerDefeatedEnemy = 0, // 爆裂箭矢每次实际击杀提供一次红色减甲选择
        MK.Logic.Runtime.UnitState SourceUnit = null, // 自有部队产生的贡献；原单位加值不影响卡牌或自己
        object SourceCard = null, // 正式出牌的物理实例，复制效果保留宿主来源
        Monster RequiredEnemy = null, // 限定为真实敌人实例；不得转借给组内其他敌人
        SkillCard SourceSkill = null,
        bool FromUnitAbility = false
    )
    {
        public bool HasExcludedTarget => ExcludedEnemy != null || ExcludedTargetIndex >= 0;
        public bool Excludes(Monster enemy, int index) => ExcludedEnemy != null
            ? object.ReferenceEquals(ExcludedEnemy, enemy) : ExcludedTargetIndex >= 0 && ExcludedTargetIndex == index;
    }

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
        bool IsElite = false,      // 是否为精英单位，难民营招募时会额外加费
        int BlockValue = 0,        // 可用的基础格挡能力；未配置的单位不提供格挡
        UnitAbility[] Actions = null,
        int DamageInfluenceCost = 0,
        bool ReverseRecruitReputation = false,
        int AssaultActivationInfluenceCost = 0,
        bool DoubleRecruitReputation = false,
        bool InteractionRecruitOnly = false,
        bool RequiresAttunement = false
    );
}
