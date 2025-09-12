using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Interactions;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Scripts.Testing
{
    /// <summary>
    /// 地点交互测试控制器 - 在测试场景中管理地点交互的演示和测试
    /// </summary>
    public class LocationTestController : MonoBehaviour
    {
        [Header("UI 参考")]
        public Button villageInteractButton;
        public Button monasteryInteractButton;
        public Button mageTowerInteractButton;
        public Button keepInteractButton;
        public Button cityInteractButton;
        
        public Text villageDescriptionText;
        public Text monasteryDescriptionText;
        public Text mageTowerDescriptionText;
        public Text keepDescriptionText;
        public Text cityDescriptionText;
        
        public Text logText;
        public Text playerStateText;
        
        public Dropdown interactionTypeDropdown;
        public InputField influenceInputField;
        
        [Header("测试地图设置")]
        public List<PlaceData> testPlaces = new List<PlaceData>();
        
        private LocationInteractionManager _interactionManager;
        private PlayerState _testPlayer;
        private GameState _gameState;
        private MapState _mapState;
        private List<PlaceState> _placeStates;
        private InteractParameter _currentParameter;
        
        private void Start()
        {
            InitializeTestEnvironment();
            SetupUI();
            RefreshPlaceDescriptions();
            RefreshPlayerState();
            
            Log("Location Test Controller initialized!");
            Log("Click on location buttons to test different interactions.");
        }
        
        private void InitializeTestEnvironment()
        {
            // 创建测试数据
            InitializeTestPlayer();
            InitializeTestGameState();
            InitializeTestPlaces();
            InitializeInteractionManager();
        }
        
private void InitializeTestPlayer()
        {
            // 使用正确的构造方法创建PlayerState
            _testPlayer = new PlayerState(1, "TestPlayer");
            _testPlayer.Reputation = 3;
            _testPlayer.Fame = 5;
            _testPlayer.Wounds = 1;
            
            // PlayerState中没有这些属性，直接使用现有属性
            Log($"Test player initialized: Name={_testPlayer.Name}, Reputation={_testPlayer.Reputation}, Influence={_testPlayer.Influence}");
            
            // 创建测试牌库
            _testPlayer.Deck.InitializeStarterDeck();
            
            // PlayerState已有Units和Skills集合，无需额外初始化
            Log($"Test player decks initialized");
        }
        
private void InitializeTestGameState()
        {
            _gameState = new GameState();
            _mapState = new MapState();
            
            // GameState不包含GlobalResources，GlobalResources是独立的实例
            var globalResources = new GlobalResources(4, () => DayPart.Day, null);
            globalResources.InitializeForNewGame();
            
            Log("Test game state initialized. GlobalResources must be accessed locally or through other components.");
        }
        
        private void InitializeTestPlaces()
        {
            _placeStates = new List<PlaceState>();
            
            // 创建测试地点
            var village = new PlaceState(PlaceType.Village);
            var monastery = new PlaceState(PlaceType.Monastery);
            var keep = new PlaceState(PlaceType.Keep);
            var city = new PlaceState(PlaceType.City);
            
            _placeStates.AddRange(new[] { village, monastery, keep, city });
            
            Log($"Created {(_placeStates.Count)} test places.");
        }
        
private void InitializeInteractionManager()
        {
            // 创建招募服务
            var recruitmentService = new RecruitmentService();
            
            // 创建地点管理器
            var monsters = MK.Logic.Data.MonsterJsonLoader.LoadMonsters();
            var globalResources = new GlobalResources(4, () => DayPart.Day, null);
            var placeManager = new PlaceManager(monsters, globalResources);
            
            // 创建交互管理器（移除不存在的参数）
            _interactionManager = new LocationInteractionManager(
                globalResources,
                recruitmentService,
                placeManager
            );
            
            // 为测试地点创建交互
            foreach (var placeState in _placeStates)
            {
                _interactionManager.CreateInteractionForPlace(placeState);
            }
            
            Log("Interaction manager initialized with all test places.");
        }
        
        private void SetupUI()
        {
            // 设置按钮事件
            villageInteractButton.onClick.AddListener(OnVillageInteract);
            monasteryInteractButton.onClick.AddListener(OnMonasteryInteract);
            mageTowerInteractButton.onClick.AddListener(OnMageTowerInteract);
            keepInteractButton.onClick.AddListener(OnKeepInteract);
            cityInteractButton.onClick.AddListener(OnCityInteract);
            
            // 初始化交互类型下拉菜单
            interactionTypeDropdown.options.Clear();
            var interactionTypes = new[] { "治疗", "招募", "学习技能", "购买法术", "购买魔晶", "与领主交互", "攻城" };
            foreach (var type in interactionTypes)
            {
                interactionTypeDropdown.options.Add(new Dropdown.OptionData(type));
            }
            interactionTypeDropdown.value = 0;
            
            // 设置影响力输入框
            influenceInputField.text = "0";
        }
        
        private void OnVillageInteract()
        {
            var selectedType = (InteractType)interactionTypeDropdown.value + 1; // 跳过None
            var parameter = CreateInteractParameter(selectedType);
            
            if (_interactionManager.InteractWithPlace(PlaceType.Village, _testPlayer, parameter))
            {
                Log($"Village interaction successful: {selectedType}");
                HandleSuccessfulInteraction();
            }
            else
            {
                Log($"Village interaction failed: {selectedType}");
            }
            
            RefreshPlayerState();
        }
        
        private void OnMonasteryInteract()
        {
            var parameter = InteractParameter.CreateMonasteryHeal();
            parameter.Influence = int.Parse(influenceInputField.text);
            
            if (_interactionManager.InteractWithPlace(PlaceType.Monastery, _testPlayer, parameter))
            {
                Log("Monastery heal successful!");
                HandleSuccessfulInteraction();
            }
            else
            {
                Log("Monastery heal failed - insufficient resources or invalid conditions.");
            }
            
            RefreshPlayerState();
        }
        
        private void OnMageTowerInteract()
        {
            var parameter = InteractParameter.CreateMageTowerBuyCrystal();
            parameter.Influence = int.Parse(influenceInputField.text);
            parameter.ManaColor = ManaColor.Red; // 默认红色魔晶
            
            if (_interactionManager.InteractWithPlace(PlaceType.ManaMine, _testPlayer, parameter))
            {
                Log("Mage Tower crystal purchase successful!");
                HandleSuccessfulInteraction();
            }
            else
            {
                Log("Mage Tower crystal purchase failed - insufficient resources or invalid conditions.");
            }
            
            RefreshPlayerState();
        }
        
        private void OnKeepInteract()
        {
            var parameter = InteractParameter.CreateKeepInteract();
            
            if (_interactionManager.InteractWithPlace(PlaceType.Keep, _testPlayer, parameter))
            {
                Log("Keep interaction successful!");
                HandleSuccessfulInteraction();
            }
            else
            {
                Log("Keep interaction failed - insufficient reputation or resources.");
            }
            
            RefreshPlayerState();
        }
        
        private void OnCityInteract()
        {
            var selectedType = (InteractType)interactionTypeDropdown.value + 1;
            var parameter = CreateInteractParameter(selectedType);
            
            if (_interactionManager.InteractWithPlace(PlaceType.City, _testPlayer, parameter))
            {
                Log($"City interaction successful: {selectedType}");
                HandleSuccessfulInteraction();
            }
            else
            {
                Log($"City interaction failed: {selectedType}");
            }
            
            RefreshPlayerState();
        }
        
        private InteractParameter CreateInteractParameter(InteractType type)
        {
            var influence = int.Parse(influenceInputField.text);
            
            switch (type)
            {
                case InteractType.VillageHeal:
                    return new InteractParameter { Type = type, Influence = influence };
                    
                case InteractType.VillageRecruit:
                    return InteractParameter.CreateVillageRecruit();
                    
                case InteractType.MonasteryHeal:
                    return InteractParameter.CreateMonasteryHeal();
                    
                case InteractType.MageTowerBuyCrystal:
                    return new InteractParameter { Type = type, Influence = influence, ManaColor = ManaColor.Red };
                    
                case InteractType.KeepInteract:
                    return InteractParameter.CreateKeepInteract();
                    
                case InteractType.KeepSiege:
                    return InteractParameter.CreateKeepSiege(true); // 选择攻击
                    
                default:
                    return new InteractParameter { Type = type, Influence = influence };
            }
        }
        
        private void HandleSuccessfulInteraction()
        {
            // 播放成功音效或动画
            Log("✅ Interaction completed successfully!");
        }
        
        private void RefreshPlaceDescriptions()
        {
            try
            {
                villageDescriptionText.text = _interactionManager.GetInteractionDescription(PlaceType.Village);
                monasteryDescriptionText.text = _interactionManager.GetInteractionDescription(PlaceType.Monastery);
                mageTowerDescriptionText.text = _interactionManager.GetInteractionDescription(PlaceType.ManaMine);
                keepDescriptionText.text = _interactionManager.GetInteractionDescription(PlaceType.Keep);
                cityDescriptionText.text = _interactionManager.GetInteractionDescription(PlaceType.City);
            }
            catch (System.Exception ex)
            {
                Log($"Error refreshing descriptions: {ex.Message}");
            }
        }
        
private void RefreshPlayerState()
        {
            try
            {
                var stateText = $"玩家状态：\n";
                stateText += $"声望: {_testPlayer.Reputation}\n";
                stateText += $"影响力: {_testPlayer.Influence}\n";
                stateText += $"创伤: {_testPlayer.Wounds}\n";
                stateText += $"已招募单位: {_testPlayer.Units.Count}\n";
                stateText += $"手牌数: {_testPlayer.Deck.Hand.Count}\n";
                
                playerStateText.text = stateText;
            }
            catch (System.Exception ex)
            {
                Log($"Error refreshing player state: {ex.Message}");
            }
        }
        
        public void Log(string message)
        {
            var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\n";
            
            logText.text += logEntry;
            
            // 限制日志长度
            if (logText.text.Length > 2000)
            {
                var lines = logText.text.Split('\n');
                logText.text = string.Join("\n", lines.Skip(lines.Length - 20)); // 保留最近20行
            }
            
            Debug.Log($"[LocationTest] {message}");
        }
        
        public void ClearLog()
        {
            logText.text = "";
            Log("Log cleared.");
        }
        
        public void ResetPlayer()
        {
            InitializeTestPlayer();
            RefreshPlayerState();
            Log("Player state reset to initial values.");
        }
    }
}