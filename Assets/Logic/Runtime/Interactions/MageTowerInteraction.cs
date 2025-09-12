using MK.Logic.Core;
using MK.Logic.Runtime.Interactions;
using System.Linq;

namespace MK.Logic.Runtime.Locations
{
    /// <summary>
    /// 法师塔交互实现类
    /// </summary>
    public class MageTowerInteraction : IInteractable
    {
        private readonly PlaceState _placeState;
        private readonly GlobalResources _globalResources;
        private readonly RecruitmentService _recruitmentService;
        private readonly PlaceManager _placeManager;

        public MageTowerInteraction(PlaceState placeState, GlobalResources globalResources, RecruitmentService recruitmentService, PlaceManager placeManager)
        {
            _placeState = placeState;
            _globalResources = globalResources;
            _recruitmentService = recruitmentService;
            _placeManager = placeManager;
        }

        public bool Interact(PlayerState player, InteractParameter parameter)
        {
            if (_placeState.Type != PlaceType.ManaMine && _placeState.Type != PlaceType.DeepMine) return false;

            return parameter.Type switch
            {
                InteractType.MageTowerBuySpell => BuySpellInMageTower(player),
                InteractType.MageTowerBuyCrystal => BuyCrystalInMageTower(player, parameter),
                _ => false
            };
        }

        public bool CanInteract(PlayerState player)
        {
            return _placeState.Type == PlaceType.ManaMine || _placeState.Type == PlaceType.DeepMine;
        }

        public string GetInteractionDescription()
        {
            return "法师塔 - 购买法术, 购买魔晶(2影响)";
        }

        /// <summary>
        /// 在法师塔购买法术
        /// </summary>
        private bool BuySpellInMageTower(PlayerState player)
        {
            // 获取法术供应
            var availableSpells = _globalResources.GetAvailableSpells();
            if (availableSpells.Count == 0) return false;

            // 展示法术牌供选择
            var spellsToChoose = availableSpells.Take(3).ToList();
            
            // 这里需要UI系统支持选择法术
            // 假设选择第一张法术牌
            var selectedSpell = spellsToChoose[0];
            
            // 检查玩家是否有足够的法术购买资源
            if (player.Mana.GetTotal() < 3) return false;
            
            // 扣除法术资源成本
            player.Mana.UseMana(3);
            
            // 将法术添加到玩家手牌
            player.Deck.AddCardToHand(selectedSpell);
            
            // 从公共供应中移除
            _globalResources.RemoveSpellFromSupply(selectedSpell);
            
            return true;
        }

        /// <summary>
        /// 在法师塔购买魔晶
        /// </summary>
        private bool BuyCrystalInMageTower(PlayerState player, InteractParameter parameter)
        {
            if (parameter.Influence < 2) return false;
            if (!parameter.ManaColor.HasValue) return false;

            var manaColor = parameter.ManaColor.Value;
            
            // 检查是否是法师塔地点
            var isManaTower = _placeManager.IsMageTowerLocation(_placeState);
            if (!isManaTower) return false;

            // 获得对应颜色的魔晶
            player.Mana.AddCrystal(manaColor, 1);
            
            // 消耗影响力资源
            // player.Influence -= 2; // TODO: 等待资源系统完善
            
            return true;
        }
    }
}