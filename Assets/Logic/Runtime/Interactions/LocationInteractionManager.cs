using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime.Locations;
using MK.Logic.Runtime.Interactions;

namespace MK.Logic.Runtime.Interactions
{
    /// <summary>
    /// 地点交互管理器 - 统一管理和创建各种地点的交互实现
    /// </summary>
    public class LocationInteractionManager
    {
        private readonly Dictionary<PlaceType, IInteractable> _interactions;
        private readonly GlobalResources _globalResources;
        private readonly RecruitmentService _recruitmentService;
        private readonly PlaceManager _placeManager;

        public LocationInteractionManager(
            GlobalResources globalResources,
            RecruitmentService recruitmentService,
            PlaceManager placeManager)
        {
            _globalResources = globalResources;
            _recruitmentService = recruitmentService;
            _placeManager = placeManager;
            _interactions = new Dictionary<PlaceType, IInteractable>();
        }

        /// <summary>
        /// 为指定地点创建交互实现
        /// </summary>
        public void CreateInteractionForPlace(PlaceState placeState)
        {
            if (placeState == null) return;

            switch (placeState.Type)
            {
                case PlaceType.Village:
                    var villageInteraction = new VillageInteraction(placeState, _placeManager, _recruitmentService);
                    _interactions[PlaceType.Village] = villageInteraction;
                    break;

                case PlaceType.Monastery:
                    var monasteryInteraction = new MonasteryInteraction(placeState, _globalResources);
                    _interactions[PlaceType.Monastery] = monasteryInteraction;
                    break;

                case PlaceType.ManaMine:
                case PlaceType.DeepMine:
                    // 法师塔功能通过矿地点提供
                    var mageTowerInteraction = new MageTowerInteraction(placeState, _globalResources, _recruitmentService, _placeManager);
                    _interactions[placeState.Type] = mageTowerInteraction;
                    break;

                case PlaceType.Keep:
                    var keepInteraction = new KeepInteraction(placeState, _placeManager, _recruitmentService);
                    _interactions[PlaceType.Keep] = keepInteraction;
                    break;

                case PlaceType.City:
                    // 城市需要复合功能
                    var cityVillageInteraction = new VillageInteraction(placeState, _placeManager, _recruitmentService);
                    var cityMageTowerInteraction = new MageTowerInteraction(placeState, _globalResources, _recruitmentService, _placeManager);
                    var cityKeepInteraction = new KeepInteraction(placeState, _placeManager, _recruitmentService);
                    var cityInteraction = new CityInteraction(placeState, _placeManager, cityVillageInteraction, 
                        cityMageTowerInteraction, cityKeepInteraction);
                    _interactions[PlaceType.City] = cityInteraction;
                    break;

                default:
                    // 其他地点暂时不支持交互
                    break;
            }
        }

        /// <summary>
        /// 获取指定地点的交互实现
        /// </summary>
        public IInteractable? GetInteraction(PlaceType placeType)
        {
            return _interactions.TryGetValue(placeType, out var interaction) ? interaction : null;
        }

        /// <summary>
        /// 获取所有支持的交互类型
        /// </summary>
        public List<IInteractable> GetAllInteractions()
        {
            return new List<IInteractable>(_interactions.Values);
        }

        /// <summary>
        /// 检查玩家是否可以与指定地点交互
        /// </summary>
        public bool CanInteractWithPlace(PlaceType placeType, PlayerState player)
        {
            var interaction = GetInteraction(placeType);
            return interaction?.CanInteract(player) ?? false;
        }

        /// <summary>
        /// 执行地点交互
        /// </summary>
        public bool InteractWithPlace(PlaceType placeType, PlayerState player, InteractParameter parameter)
        {
            var interaction = GetInteraction(placeType);
            return interaction?.Interact(player, parameter) ?? false;
        }

        /// <summary>
        /// 获取指定地点的交互描述
        /// </summary>
        public string GetInteractionDescription(PlaceType placeType)
        {
            var interaction = GetInteraction(placeType);
            return interaction?.GetInteractionDescription() ?? "无可用交互";
        }

        /// <summary>
        /// 清除所有交互
        /// </summary>
        public void ClearAllInteractions()
        {
            _interactions.Clear();
        }

        /// <summary>
        /// 移除指定类型的交互
        /// </summary>
        public bool RemoveInteraction(PlaceType placeType)
        {
            return _interactions.Remove(placeType);
        }
    }
}