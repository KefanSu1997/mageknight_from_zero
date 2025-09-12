using MK.Logic.Core;
using MK.Logic.Runtime.Interactions;

namespace MK.Logic.Runtime.Locations
{
    /// <summary>
    /// 村庄交互实现类
    /// </summary>
    public class VillageInteraction : IInteractable
    {
        private readonly PlaceState _placeState;
        private readonly PlaceManager _placeManager;
        private readonly RecruitmentService _recruitmentService;

        public VillageInteraction(PlaceState placeState, PlaceManager placeManager, RecruitmentService recruitmentService)
        {
            _placeState = placeState;
            _placeManager = placeManager;
            _recruitmentService = recruitmentService;
        }

        public bool Interact(PlayerState player, InteractParameter parameter)
        {
            if (_placeState.Type != PlaceType.Village) return false;

            return parameter.Type switch
            {
                InteractType.VillageHeal => HealInVillage(player, parameter),
                InteractType.VillageRecruit => RecruitInVillage(player),
                InteractType.VillagePlunder => PlunderVillage(player),
                _ => false
            };
        }

        public bool CanInteract(PlayerState player)
        {
            if (_placeState.Type != PlaceType.Village) return false;
            return true;
        }

        public string GetInteractionDescription()
        {
            return "村庄 - 治疗(3影响), 招募新兵, 洗劫(-1声望)";
        }

        /// <summary>
        /// 在村庄进行治疗
        /// </summary>
        private bool HealInVillage(PlayerState player, InteractParameter parameter)
        {
            if (parameter.Influence < 3) return false;
            if (player.Wounds <= 0) return false;

            player.Wounds -= 1;
            // 这里需要减少影响力资源
            // player.Influence -= 3; // TODO: 等待资源系统完善
            return true;
        }

        /// <summary>
        /// 在村庄招募
        /// </summary>
        private bool RecruitInVillage(PlayerState player)
        {
            var availableUnits = _recruitmentService.GetAvailableUnits(RecruitLocation.Village);
            if (availableUnits.Count == 0) return false;

            var recruitCosts = _recruitmentService.GetRecruitCosts(RecruitLocation.Village);
            if (player.Influence < recruitCosts.reputationCost) return false; // TODO: 更精细的检查

            // 假设选择第一个可用单位
            var unitCard = availableUnits[0];
            var unitState = new UnitState(unitCard);
            player.Units.Add(unitState);
            return true;
        }

        /// <summary>
        /// 洗劫村庄
        /// </summary>
        private bool PlunderVillage(PlayerState player)
        {
            // 检查守卫战斗或声望惩罚
            if (player.Reputation < -3)
            {
                // 与守卫发生战斗
                var guardBattle = _placeManager.TriggerGuardBattle(_placeState, player);
                if (!guardBattle.success) return false;
            }

            // 抽两张牌并降低声望
            player.Deck.DrawExact(2);
            player.Reputation -= 1;
            return true;
        }
    }
}