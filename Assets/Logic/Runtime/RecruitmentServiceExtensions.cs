using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public static class RecruitmentServiceExtensions
    {
        /// <summary>
        /// 获取指定地点可用的招募单位
        /// </summary>
        public static List<UnitCard> GetAvailableUnits(this RecruitmentService service, RecruitLocation location)
        {
            // TODO: 需要实现具体的数据获取逻辑
            return new List<UnitCard>
            {
                new UnitCard("village_guard", "村庄守卫", "Village Guard", 1, 2, 2, RecruitLocation.Village, 
                    new[] { new AttackProfile(AttackType.Melee, 2, Element.Physical) }, 
                    System.Array.Empty<Ability>()),
                new UnitCard("village_archer", "村庄弓箭手", "Village Archer", 1, 2, 3, RecruitLocation.Village,
                    new[] { new AttackProfile(AttackType.Ranged, 3, Element.Physical) },
                    System.Array.Empty<Ability>())
            };
        }

        /// <summary>
        /// 获取某个地点的招募花费
        /// </summary>
        public static (int reputationCost, int influenceCost) GetRecruitCosts(this RecruitmentService service, RecruitLocation location)
        {
            // 根据地点返回相应的招募花费
            return location switch
            {
                RecruitLocation.Village => (1, 2),
                RecruitLocation.Keep => (2, 4),
                RecruitLocation.City => (3, 5),
                RecruitLocation.Monastery => (1, 3),
                RecruitLocation.MageTower => (2, 4),
                RecruitLocation.RefugeeCamp => (0, 2),
                _ => (1, 2)
            };
        }

        /// <summary>
        /// 获取精英单位
        /// </summary>
        public static List<UnitCard> GetAvailableEliteUnits(this RecruitmentService service, RecruitLocation location)
        {
            return new List<UnitCard>
            {
                new UnitCard("elite_guard", "精英守卫", "Elite Guard", 2, 4, 6, RecruitLocation.City | RecruitLocation.Keep,
                    new[] { new AttackProfile(AttackType.Melee, 4, Element.Physical) },
                    new[] { Ability.Enduring, Ability.Guard }, true),
                new UnitCard("knight", "骑士", "Knight", 2, 5, 5, RecruitLocation.Keep,
                    new[] { new AttackProfile(AttackType.Melee, 5, Element.Physical) },
                    new[] { Ability.Enduring }, true)
            };
        }
    }
}