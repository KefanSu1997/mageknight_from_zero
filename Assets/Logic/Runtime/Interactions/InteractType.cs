namespace MK.Logic.Runtime.Interactions
{
    /// <summary>
    /// 交互类型枚举，表示不同地点可以执行的交互行为
    /// </summary>
    public enum InteractType
    {
        None = 0,

        // 村庄交互
        VillageHeal,
        VillageRecruit,
        VillagePlunder,

        // 修道院交互
        MonasteryHeal,
        MonasteryLearnSkill,

        // 法师塔交互
        MageTowerBuySpell,
        MageTowerBuyCrystal,

        // 要塞交互
        KeepInteract,
        KeepSiege,

        // 城市交互（综合交互）
        CityInteraction,

        // 特殊交互的子类型
        SubType = 100
    }
}