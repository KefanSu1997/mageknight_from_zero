namespace MK.Logic.Data
{
    /// <summary>封装一次最终对英雄 / 单位结算的伤害</summary>
    public sealed record DamagePacket(
        int RawDamage,          // 格挡后仍剩余的攻击值
        bool IsPoison );        // 怪物是否带 Poison
}
