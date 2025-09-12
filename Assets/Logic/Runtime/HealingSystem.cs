namespace MK.Logic.Runtime
{
    /// <summary>
    /// 处理部队与英雄的治疗与解雇逻辑。
    /// </summary>
    public static class HealingSystem
    {
        /// <summary>治疗指定单位的伤口。</summary>
        public static void HealUnit(UnitState unit, int healPoints)
        {
            unit.Heal(healPoints);
        }

        /// <summary>从玩家队列中移除一支部队。</summary>
        public static void DismissUnit(PlayerState player, UnitState unit)
        {
            player.Units.Remove(unit);
        }
    }
}
