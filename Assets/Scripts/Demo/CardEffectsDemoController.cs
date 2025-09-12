using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;

namespace MK.Demo
{
    public class CardEffectsDemoController : MonoBehaviour
    {
        [Header("UI References")]
        public Transform cardContainer;
        public Text effectLogText;
        public Button triggerEffectButton;
        public Dropdown effectCategoryDropdown;
        public Text playerStatsText;
        public Text gameStateText;
        
        [Header("Card Prefabs")]
        public GameObject cardPrefab;
        
        [Header("Visual Effects")]
        public GameObject healEffectPrefab;
        public GameObject damageEffectPrefab;
        public GameObject manaEffectPrefab;
        
        private List<CardEffectTestData> _availableEffects;
        private CardEffectTestData _selectedEffect;
        private PlayerState _demoPlayer;
        private GameEngine _gameEngine;
        private List<string> _effectLog = new List<string>();
        
        [System.Serializable]
        public class CardEffectTestData
        {
            public string name;
            public string description;
            public string category;
            public ICardEffect effect;
            public Color cardColor;
        }
        
        void Start()
        {
            InitializeDemo();
            SetupUI();
            PopulateEffects();
            CreateEffectCards();
            UpdateUI();
        }
        
        void InitializeDemo()
        {
            _demoPlayer = new PlayerState(1, "DemoPlayer");
            _gameEngine = new GameEngine(new List<PlayerState> { _demoPlayer }, 5);
            
            // 设置初始状态
            _demoPlayer.Wounds = 0;
            // 注意：ManaPool和Deck已经在PlayerState构造函数中初始化
            
            Log("卡牌效果演示初始化完成");
        }
        
        void SetupUI()
        {
            triggerEffectButton.onClick.AddListener(OnTriggerEffect);
            
            // 设置效果分类下拉菜单
            effectCategoryDropdown.ClearOptions();
            effectCategoryDropdown.AddOptions(new List<string> { 
                "全部", "攻击", "防御", "资源", "特殊", "召唤", "控制" 
            });
            effectCategoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        }
        
        void PopulateEffects()
        {
            _availableEffects = new List<CardEffectTestData>();
            
            // 攻击类效果
            _availableEffects.Add(new CardEffectTestData
            {
                name = "火球术",
                description = "造成3点火焰伤害",
                category = "攻击",
                effect = new FireballEffect(false),
                cardColor = Color.red
            });
            
            _availableEffects.Add(new CardEffectTestData
            {
                name = "闪电",
                description = "造成2点闪电伤害并抽1张牌",
                category = "攻击",
                effect = new ManaBoltEffect(false),
                cardColor = Color.yellow
            });
            
            // 防御类效果
            _availableEffects.Add(new CardEffectTestData
            {
                name = "治疗",
                description = "恢复3点生命值",
                category = "防御",
                effect = new HealEffect(3),
                cardColor = Color.green
            });
            
            _availableEffects.Add(new CardEffectTestData
            {
                name = "冰盾",
                description = "获得2点护甲",
                category = "防御",
                effect = new IceShieldEffect(false),
                cardColor = Color.cyan
            });
            
            // 资源类效果
            _availableEffects.Add(new CardEffectTestData
            {
                name = "魔力汲取",
                description = "获得2点魔力",
                category = "资源",
                effect = new ManaDrawEffect(false),
                cardColor = Color.blue
            });
            
            _availableEffects.Add(new CardEffectTestData
            {
                name = "卡牌抽取",
                description = "抽2张牌",
                category = "资源",
                effect = new LearningEffect(false),
                cardColor = Color.magenta
            });
            
            // 特殊类效果
            _availableEffects.Add(new CardEffectTestData
            {
                name = "传送",
                description = "移动到相邻地块",
                category = "特殊",
                effect = new WarpEffect(false),
                cardColor = Color.white
            });
            
            _availableEffects.Add(new CardEffectTestData
            {
                name = "召唤",
                description = "召唤一个2/2的单位",
                category = "召唤",
                effect = new BattleSummonsEffect(false),
                cardColor = Color.gray
            });
            
            // 控制类效果
            _availableEffects.Add(new CardEffectTestData
            {
                name = "冰冻",
                description = "冻结目标1回合",
                category = "控制",
                effect = new FreezeEffect(false),
                cardColor = Color.cyan
            });
            
            _availableEffects.Add(new CardEffectTestData
            {
                name = "恐惧",
                description = "使目标攻击力-2",
                category = "控制",
                effect = new ThreatenEffect(false),
                cardColor = Color.black
            });
        }
        
        void CreateEffectCards()
        {
            // 清理现有卡片
            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 创建效果卡片
            foreach (var effectData in _availableEffects)
            {
                var cardObj = Instantiate(cardPrefab, cardContainer);
                var cardUI = cardObj.GetComponent<CardEffectUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(effectData, OnCardSelected);
                }
                
                // 设置卡片颜色
                var image = cardObj.GetComponent<Image>();
                if (image != null)
                {
                    image.color = effectData.cardColor * 0.8f;
                }
            }
        }
        
        void OnCardSelected(CardEffectTestData effectData)
        {
            _selectedEffect = effectData;
            Log($"选择了效果: {effectData.name}");
            
            // 高亮选中的卡片
            foreach (Transform child in cardContainer)
            {
                var cardUI = child.GetComponent<CardEffectUI>();
                if (cardUI != null)
                {
                    cardUI.SetSelected(cardUI.EffectData == effectData);
                }
            }
        }
        
        void OnTriggerEffect()
        {
            if (_selectedEffect != null)
            {
                try
                {
                    Log($"触发效果: {_selectedEffect.name}");
                    
                    // 创建测试上下文
                    var context = new ActionContext();
                    
                    // 执行效果
                    _selectedEffect.effect.Execute(_demoPlayer, context);
                    
                    Log($"效果执行成功: {_selectedEffect.name}");
                    ShowEffectVisual(_selectedEffect);
                    
                    UpdateUI();
                }
                catch (System.Exception ex)
                {
                    Log($"效果执行异常: {ex.Message}");
                }
            }
            else
            {
                Log("请先选择一个效果");
            }
        }
        
        void OnCategoryChanged(int categoryIndex)
        {
            string selectedCategory = effectCategoryDropdown.options[categoryIndex].text;
            
            // 过滤显示卡片
            foreach (Transform child in cardContainer)
            {
                var cardUI = child.GetComponent<CardEffectUI>();
                if (cardUI != null)
                {
                    bool shouldShow = selectedCategory == "全部" || 
                                    cardUI.EffectData.category == selectedCategory;
                    child.gameObject.SetActive(shouldShow);
                }
            }
        }
        
        void ShowEffectVisual(CardEffectTestData effectData)
        {
            GameObject effectPrefab = null;
            
            switch (effectData.category)
            {
                case "攻击":
                    effectPrefab = damageEffectPrefab;
                    break;
                case "防御":
                    effectPrefab = healEffectPrefab;
                    break;
                case "资源":
                    effectPrefab = manaEffectPrefab;
                    break;
            }
            
            if (effectPrefab != null)
            {
                var effectObj = Instantiate(effectPrefab, transform);
                effectObj.transform.position = new Vector3(0, 2, 0);
                
                // 添加粒子系统
                var particles = effectObj.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    particles.Play();
                }
                
                Destroy(effectObj, 3f);
            }
        }
        
        void UpdateUI()
        {
            // 更新玩家状态
            playerStatsText.text = $"玩家状态:\n" +
                                  $"生命值: {_demoPlayer.Armor - _demoPlayer.Wounds}\n" +
                                  $"魔力值: {_demoPlayer.Mana.GetTotal()}\n" +
                                  $"手牌数: {_demoPlayer.Deck.Hand.Count}\n" +
                                  $"牌库数: {_demoPlayer.Deck.DrawPileCount}";
            
            // 更新游戏状态
            gameStateText.text = $"游戏状态:\n" +
                               $"当前回合: 演示回合\n" +
                               $"活跃玩家: 演示玩家\n" +
                               $"阶段: 演示阶段";
            
            // 更新效果日志
            var recentLogs = _effectLog.Count > 8 ? _effectLog.Skip(_effectLog.Count - 8).ToArray() : _effectLog.ToArray();
            effectLogText.text = string.Join("\n", recentLogs);
        }
        
        void Log(string message)
        {
            _effectLog.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log(message);
            UpdateUI();
        }
        
        public void ResetDemo()
        {
            InitializeDemo();
            _effectLog.Clear();
            Log("演示已重置");
            UpdateUI();
        }
    }
    
    public class CardEffectUI : MonoBehaviour
    {
        public Text nameText;
        public Text descriptionText;
        public Image backgroundImage;
        
        private CardEffectsDemoController.CardEffectTestData _effectData;
        private System.Action<CardEffectsDemoController.CardEffectTestData> _onSelected;
        
        public void Initialize(CardEffectsDemoController.CardEffectTestData effectData, 
                             System.Action<CardEffectsDemoController.CardEffectTestData> onSelected)
        {
            _effectData = effectData;
            _onSelected = onSelected;
            
            if (nameText != null)
                nameText.text = effectData.name;
            
            if (descriptionText != null)
                descriptionText.text = effectData.description;
            
            if (backgroundImage != null)
                backgroundImage.color = effectData.cardColor;
        }
        
        public CardEffectsDemoController.CardEffectTestData EffectData => _effectData;
        
        public void SetSelected(bool selected)
        {
            if (backgroundImage != null)
            {
                var color = selected ? Color.white : _effectData.cardColor;
                backgroundImage.color = color;
            }
        }
        
        public void OnPointerClick()
        {
            _onSelected?.Invoke(_effectData);
        }
    }
}