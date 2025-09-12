using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Interactions
{
    /// <summary>
    /// 简化版的单位数据，用于位置交互的测试和实现
    /// </summary>
    public class SimpleUnit
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Armor { get; set; } = 0;
        public int Attack { get; set; } = 0;
        public int RangedAttack { get; set; } = 0;
        public int Block { get; set; } = 0;
        public int Health { get; set; } = 5;
        public int Wounds { get; set; } = 0;
        public int Spell { get; set; } = 0;
        public List<Ability> Abilities { get; set; } = new List<Ability>();
        public bool IsElite { get; set; } = false;
        public int InfluenceCost { get; set; } = 3;
        public RecruitLocation RecruitLocationMask { get; set; } = RecruitLocation.Village;
    }

    /// <summary>
    /// 简化版的神器卡牌
    /// </summary>
    public class SimpleArtifact
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Fame { get; set; } = 0;
        public string CardText { get; set; } = "";
    }

    /// <summary>
    /// Monster扩展，添加一些交互用的属性
    /// </summary>
    public static class MonsterExtensions
    {
        public static Monster CreateGuard(string name = "地点守卫")
        {
            return new Monster(MonsterType.Orc, new AxialCoord(0, 0))
            {
                Force = 4,
                Hull = 6
            };
        }

        public static Monster CreateElite(string name = "精英守卫")
        {
            return new Monster(MonsterType.Dragon, new AxialCoord(0, 0))
            {
                Force = 8,
                Hull = 10
            };
        }
    }
}