using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Interactions;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Locations
{
    /// <summary>
    /// 要塞交互实现类
    /// </summary>
    public class KeepInteraction : IInteractable
    {
        private readonly PlaceState _placeState;
        private List<MK.Logic.Data.Monster> _recruitedUnits;
private readonly PlaceManager _placeManager;
        private readonly RecruitmentService _recruitmentService;

        public KeepInteraction(PlaceState placeState, PlaceManager placeManager, 
            RecruitmentService recruitmentService)
        {
            _placeState = placeState;
            _placeManager = placeManager;
            _recruitedUnits = new List<MK.Logic.Data.Monster>();
_recruitmentService = recruitmentService;
        }

        public bool Interact(PlayerState player, InteractParameter parameter)
        {
            if (_placeState.Type != PlaceType.Keep) return false;

            return parameter.Type switch
            {
                InteractType.KeepInteract => InteractWithLord(player),
                InteractType.KeepSiege => SiegeKeep(player, parameter),
                _ => false
            };
        }

        public bool CanInteract(PlayerState player)
        {
            if (_placeState.Type != PlaceType.Keep) return false;
            return true;
        }

        public string GetInteractionDescription()
        {
            return "要塞 - 与领主交互, 攻城战";
        }

        /// <summary>
        /// 与领主交互
        /// </summary>
private bool InteractWithLord(PlayerState player)
        {
            // 根据声望决定交互效果
            if (player.Reputation >= 4)
            {
                // 高声望：可以招募精英单位
                var eliteUnits = _recruitmentService.GetAvailableEliteUnits(RecruitLocation.Keep);
                if (eliteUnits.Count == 0) return false;

                // 检查招募限定 - 改变操作符为等于
                if (eliteUnits.Count == 0) return false;

                var eliteUnit = eliteUnits[0];
                var recruitCost = _recruitmentService.GetRecruitCosts(RecruitLocation.Keep);
                
                // 检查玩家是否有足够资源
                if (player.Influence >= recruitCost.reputationCost)
                {
                    var unitState = new UnitState(eliteUnit);
                    player.Units.Add(unitState);
                    // 这里需要添加影响力消耗等逻辑
                }
            }
            else if (player.Reputation >= 1)
            {
                // 中等声望：提升声望
                player.Reputation += 1;
            }
            else
            {
                // 低声望：需要支付或以其他方式获得允许
                if (player.Influence >= 3)
                {
                    // player.Influence -= 3; // TODO: 等待资源系统完善
                    player.Reputation += 1;
                }
                else
                {
                    return false; // 无法进行有效交互
                }
            }

            return true;
        }

        /// <summary>
        /// 攻城战
        /// </summary>
private bool SiegeKeep(PlayerState player, InteractParameter parameter)
        {
            if (!parameter.ChooseAttack) return false;

            var keepDefenders = GenerateKeepDefenders();
            var battleInitiative = DetermineSiegeInitiative(player, keepDefenders);
            
            // 简化的攻城判断
            var battleSuccess = battleInitiative && player.GetTotalAttackPower() >= 15;
            
            if (battleSuccess)
            {
                player.Reputation += 3;
                player.Fame += 5;
                _placeManager.ConquerPlace(_placeState);
                
                // 将精英单位转换为 UnitState 并加入玩家
                foreach (var defender in keepDefenders)
                {
                    var unitState = new UnitState(defender);
                    player.Units.Add(unitState);
                }
                player.Fame += 2;
            }
            else
            {
                player.Wounds += 2;
                player.Reputation -= 2;
            }

            return true;
        }

        /// <summary>
        /// 生成要塞守卫部队
        /// </summary>
private List<UnitCard> GenerateKeepDefenders()
        {
            return new List<UnitCard>
            {
                new UnitCard("keep_guard", "要塞守卫", "Keep Guard", 2, 4, 4, RecruitLocation.Keep,
                    new[] { new AttackProfile(AttackType.Melee, 6, Element.Physical) },
                    new[] { Ability.Guard }),
                new UnitCard("keep_archer", "要塞弩手", "Keep Archer", 2, 3, 5, RecruitLocation.Keep,
                    new[] { new AttackProfile(AttackType.Ranged, 4, Element.Physical) },
                    System.Array.Empty<Ability>())
            };
        }

        /// <summary>
        /// 确定攻城战主动权
        /// </summary>
private bool DetermineSiegeInitiative(PlayerState player, List<UnitCard> defenders)
        {
            var playerPower = player.GetTotalAttackPower() + player.Reputation;
            var defenderPower = 0;
            foreach (var unit in defenders)
            {
                foreach (var attack in unit.Attacks)
                {
                    defenderPower += attack.Value;
                }
            }
            
            return playerPower > defenderPower;
        }

        /// <summary>
        /// 从要塞生成精英单位 - 简化版本，返回可供招募的单位ID
        /// </summary>


        /// <summary>
        /// 生成要塞战利品
        /// </summary>

    }
}