// ────────────────────────────────────────────────
// Core/Enums.cs
// ------------------------------------------------
namespace MK.Logic.Core
{
    /// <summary>元素类型；ColdFire 既视为火又视为冰（规则书 p.25）</summary>
    public enum Element { Physical, Fire, Ice, ColdFire }

    /// <summary>
    /// 法力颜色枚举，含六种骰面：红、蓝、绿、白、金、黑。
    /// 供公共魔力池與法力相關系統使用。
    /// </summary>
    public enum ManaColor { Red, Blue, Green, White, Gold, Black }

    /// <summary>攻击方式；Siege 在战斗第一阶段即可作用于 Fortified 敌人</summary>
    public enum AttackType { Ranged, Siege, Melee }

    /// <summary>玩家行动牌的类型分类。</summary>
    public enum CardType { Action, Wound, Skill, Artifact, Spell }

    /// <summary>
    /// 牌组来源分類，用於對應不同的 JSON 檔案。
    /// </summary>
    public enum CardSet { BasicAction, AdvancedAction, Item, Spell }

    /// <summary>战斗阶段枚举，严格对应规则书中的 4+1 子阶段</summary>
    public enum Phase { None, Ranged, Block, AssignDamage, Melee, End }

    /// <summary>
    /// 表示当前是白天还是黑夜，用于RoundClock的日/夜循环
    /// </summary>
    public enum DayPart
    {
        Day,
        Night
    }

    /// <summary>怪物标记中的能力图标</summary>
    public enum Ability
    {
        Fortified,   // 灰塔 + 黑盾
        Swift,       // 翅膀
        Brutal,      // 残暴：若未格挡，伤害翻倍
        Poison,      // 骷髅
        Paralyze,    // 破碎胸像
        Regenerate,  // 绿色叶
        FireResist,  // 火抗
        IceResist,   // 冰抗
        ColdFireResist,
        MagicResist,  // 紫色法阵

        // 以下能力用于玩家招募的部队卡
        Guard,       // 守护：允许其在格挡阶段代替英雄受击
        Heal,        // 治疗：可移除创伤
        Negate,      // 否决：取消特殊效果
        Sweep,       // 范围：近战阶段可同时攻击多个目标
        Enduring     // 坚毅：受到创伤时降低数量
    }

    /// <summary>
    /// 单位在回合中的就绪状态。
    /// </summary>
    public enum UnitStatus { Ready, Exhausted, Fatigued }

    /// <summary>
    /// 招募地点的掩码枚举，可按位组合（Village | Keep ...）。
    /// </summary>
    [System.Flags]
    public enum RecruitLocation
    {
        None      = 0,
        Village   = 1 << 0,
        Keep      = 1 << 1,
        Monastery = 1 << 2,
        City      = 1 << 3,
        /// <summary>魔法塔，可招募常规单位与学习法术</summary>
        MageTower = 1 << 4,
        /// <summary>难民营，视为拥有所有招募图标</summary>
        RefugeeCamp = 1 << 5
    }

    /// <summary>
    /// 地形类型枚举，用于地图瓷砖与移动计算。
    /// </summary>
    public enum TerrainType
    {
        Plains,
        Hills,
        Forest,
        Desert,
        Swamp,
        Wasteland,
        Lake,
        River,
        Mountain,
        Village,
        City,
        Keep,
        Ruins
    }


    /// <summary>
    /// 地图瓷砖所属的牌堆分类。
    /// </summary>
    public enum TileSet { Countryside, Core, City }

    /// <summary>
    /// 行動牌橫置時指定的增益類型。
    /// </summary>
    public enum SidewaysType { Move, Influence, Block, Attack }

    /// <summary>
    /// 特殊地點類型，供地圖與效果系統使用。
    /// </summary>
    public enum PlaceType
    {
        OrcRampaging,
        Dragon,
        Monastery,
        Village,
        /// <summary>城牆，跨越時需要額外的移動力</summary>
        CityWall,
        /// <summary>要塞，可以招募精英單位或攻城</summary>
        Keep,
        /// <summary>城市，可以進行多種交互</summary>
        City,
        /// <summary>遠古遺跡，包含祭壇或敵人</summary>
        AncientRuin,
        /// <summary>地下城，進入後與棕色敵人戰鬥</summary>
        Dungeon,
        /// <summary>單色魔晶礦山</summary>
        ManaMine,
        /// <summary>雙色深層礦山</summary>
        DeepMine
    }

    /// <summary>
    /// 遠古遺跡翻開後可能出現的黃色標記類型。
    /// </summary>
    public enum RuinTokenType { Altar, Enemy }
}
