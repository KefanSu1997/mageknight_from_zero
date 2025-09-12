using MK.Logic.Core;
using MK.Logic.Runtime.Interactions;
using System.Linq;

namespace MK.Logic.Runtime.Locations
{
    /// <summary>
    /// 修道院交互实现类
    /// </summary>
    public class MonasteryInteraction : IInteractable
    {
        private readonly PlaceState _placeState;
        private readonly GlobalResources _globalResources;

        public MonasteryInteraction(PlaceState placeState, GlobalResources globalResources)
        {
            _placeState = placeState;
            _globalResources = globalResources;
        }

        public bool Interact(PlayerState player, InteractParameter parameter)
        {
            if (_placeState.Type != PlaceType.Monastery) return false;

            return parameter.Type switch
            {
                InteractType.MonasteryHeal => DeepHealInMonastery(player, parameter),
                InteractType.MonasteryLearnSkill => LearnSkillInMonastery(player),
                _ => false
            };
        }

        public bool CanInteract(PlayerState player)
        {
            if (_placeState.Type != PlaceType.Monastery) return false;
            return true;
        }

        public string GetInteractionDescription()
        {
            return "修道院 - 深度治疗(6影响), 学习技能";
        }

        /// <summary>
        /// 修道院深度治疗
        /// </summary>
        private bool DeepHealInMonastery(PlayerState player, InteractParameter parameter)
        {
            if (parameter.Influence < 6) return false;

            // 修道院治疗可以移除所有创伤
            // player.Wounds = 0; // TODO: 需要PlayerState支持设置Wounds
            
            // 遍历所有招募的单位，移除它们的创伤
            foreach (var unit in player.Units)
            {
                unit.Heal(100); // 使用UnitState的Heal方法来治疗所有创伤
            }

            // 消耗影响力资源
            // player.Influence -= 6; // TODO: 等待资源系统完善
            return true;
        }

        /// <summary>
        /// 在修道院学习技能
        /// </summary>
        private bool LearnSkillInMonastery(PlayerState player)
        {
            // 获取修道院技能牌供应
            var availableSkills = _globalResources.GetAvailableSkills(CardSet.BasicAction);
            if (availableSkills.Count == 0) return false;

            // 提供两张技能牌供选择
            var skillsToChoose = availableSkills.Take(2).ToList();
            
            // 这里需要UI系统支持选择技能
            // 假设选择第一张技能牌
            var selectedSkill = skillsToChoose[0];
            
            // 将技能添加到玩家的技能牌库
            player.Skills.Add(selectedSkill);
            
            // 从公共供应中移除
            // TODO: 查找要移除的具体技能牌
            
            return true;
        }
    }
}