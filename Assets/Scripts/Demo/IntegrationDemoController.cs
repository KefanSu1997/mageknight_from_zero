using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Demo
{
    public class IntegrationDemoController : MonoBehaviour
    {
        [Header("Game State UI")]
        public Text gameStateText;
        public Text turnInfoText;
        public Text playerStatusText;
        public Button nextTurnButton;
        public Button resetGameButton;
        
        [Header("Map Display")]
        public Transform mapContainer;
        public GameObject hexTilePrefab;
        public GameObject playerTokenPrefab;
        public float hexSize = 1f;
        
        [Header("Card Display")]
        public Transform handContainer;
        public GameObject cardDisplayPrefab;
        public Button drawCardButton;
        public Button playCardButton;
        
        [Header("Combat Display")]
        public GameObject combatPanel;
        public Text combatLogText;
        public Slider playerHealthSlider;
        public Slider enemyHealthSlider;
        
        [Header("Debug Panel")]
        public GameObject debugPanel;
        public Text debugLogText;
        public Button toggleDebugButton;
        
        private GameEngine _gameEngine;
        private MapState _mapState;
        private PlayerState _currentPlayer;
        private List<PlayerState> _players;
        private Dictionary<AxialCoord, GameObject> _hexObjects;
        private Dictionary<AxialCoord, MapTile> _exploredTiles;
        private AxialCoord _currentPlayerPosition;
        
        private List<string> _gameLog = new List<string>();
        private bool _isInCombat = false;
        private UnitState _currentEnemy;
        
        void Start()
        {
            InitializeGame();
            SetupUI();
            CreateMap();
            UpdateUI();
        }
        
        void InitializeGame()
        {
            _players = new List<PlayerState>();
            _gameEngine = new GameEngine(_players, 6);
            _mapState = new MapState();
            _hexObjects = new Dictionary<AxialCoord, GameObject>();
            _exploredTiles = new Dictionary<AxialCoord, MapTile>();
            
            // 创建玩家
            _players = new List<PlayerState>();
            _currentPlayer = new PlayerState(1, "Player1");
            _currentPlayer.Wounds = 0; // 健康状态
            // ManaPool 和 Deck 已经在构造函数中初始化
            
            _players.Add(_currentPlayer);
            
            // 初始化地图
            InitializeMap();
            
            // 设置初始位置
            _currentPlayerPosition = new AxialCoord(0, 0);
            
            Log("游戏初始化完成");
            Log($"玩家: {_currentPlayer.Name}, 血量: {_currentPlayer.Armor - _currentPlayer.Wounds}");
        }
        
        PlayerDeck CreateDemoDeck()
        {
            var deck = new PlayerDeck();
            // 添加测试卡牌
            var cards = new List<DeedCard>();
            for (int i = 0; i < 10; i++)
            {
                var card = new DeedCard($"test_card_{i}", CardType.Spell);
                cards.Add(card);
            }
            deck.SetDeck(cards);
            return deck;
        }
        
        void InitializeMap()
        {
            _mapState.Countryside = new TileDeck();
            _mapState.Core = new TileDeck();
            
            // 添加测试地块
            for (int i = 0; i < 5; i++)
            {
                var edges = new TerrainType[6];
                for (int j = 0; j < 6; j++)
                {
                    edges[j] = (TerrainType)((i + j) % 6);
                }
                var tile = new MapTile(TileSet.Core, i, edges);
                _mapState.Countryside.Push(tile);
            }
        }
        
        void SetupUI()
        {
            nextTurnButton.onClick.AddListener(OnNextTurn);
            resetGameButton.onClick.AddListener(ResetGame);
            drawCardButton.onClick.AddListener(OnDrawCard);
            playCardButton.onClick.AddListener(OnPlayCard);
            toggleDebugButton.onClick.AddListener(ToggleDebugPanel);
            
            combatPanel.SetActive(false);
            debugPanel.SetActive(false);
        }
        
        void CreateMap()
        {
            // 创建地图网格
            for (int q = -3; q <= 3; q++)
            {
                for (int r = -3; r <= 3; r++)
                {
                    if (Mathf.Abs(q + r) <= 3)
                    {
                        var coord = new AxialCoord(q, r);
                        CreateHexTile(coord);
                    }
                }
            }
            
            // 创建玩家棋子
            CreatePlayerToken();
        }
        
        void CreateHexTile(AxialCoord coord)
        {
            var hexPos = HexToWorldPosition(coord);
            var hexObj = Instantiate(hexTilePrefab, hexPos, Quaternion.identity, mapContainer);
            hexObj.name = $"Hex_{coord.Q}_{coord.R}";
            
            // 设置初始颜色
            var renderer = hexObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.gray;
            }
            
            _hexObjects[coord] = hexObj;
        }
        
        void CreatePlayerToken()
        {
            var playerPos = HexToWorldPosition(_currentPlayerPosition) + Vector3.up * 0.2f;
            var token = Instantiate(playerTokenPrefab, playerPos, Quaternion.identity, mapContainer);
            token.name = "PlayerToken";
        }
        
        void OnNextTurn()
        {
            if (_isInCombat)
            {
                ResolveCombat();
                return;
            }
            
            // 执行回合流程
            ExecuteTurn();
        }
        
        void ExecuteTurn()
        {
            Log("=== 开始新回合 ===");
            
            // 1. 回合开始阶段
            Log("回合开始阶段");
            _currentPlayer.Mana.ResetTokens();
            DrawInitialCards();
            
            // 2. 行动阶段
            Log("行动阶段");
            if (ShouldTriggerCombat())
            {
                StartCombat();
            }
            else if (ShouldExplore())
            {
                ExploreMap();
            }
            else
            {
                Log("等待玩家行动...");
            }
            
            // 3. 回合结束阶段
            Log("回合结束阶段");
            
            UpdateUI();
        }
        
        void DrawInitialCards()
        {
            _currentPlayer.Deck.DrawToLimit(3);
            
            foreach (var card in _currentPlayer.Deck.Hand)
            {
                Log($"抽到卡牌: {card.Id}");
            }
            
            UpdateHandDisplay();
        }
        
        void OnDrawCard()
        {
            if (_currentPlayer.Deck.DrawPileCount > 0)
            {
                var card = _currentPlayer.Deck.DrawExact(1);
                if (card > 0)
                {
                    Log($"抽到卡牌: {_currentPlayer.Deck.Hand.Last().Id}");
                    UpdateHandDisplay();
                }
            }
            else
            {
                Log("牌库已空！");
            }
        }
        
        void OnPlayCard()
        {
            if (_currentPlayer.Deck.Hand.Count > 0)
            {
                var card = _currentPlayer.Deck.Hand[0];
                _currentPlayer.Deck.Discard(card);
                Log($"使用卡牌: {card.Id}");
                
                // 模拟卡牌效果
                if (card.Id.Contains("heal"))
                {
                    _currentPlayer.Wounds = Mathf.Max(0, _currentPlayer.Wounds - 3);
                    Log("恢复3点生命值");
                }
                else if (card.Id.Contains("attack"))
                {
                    if (_isInCombat)
                    {
                        _currentEnemy.AddWounds(5);
                        Log("对敌人造成5点伤害");
                        UpdateCombatUI();
                    }
                }
                
                UpdateHandDisplay();
                UpdateUI();
            }
        }
        
        bool ShouldTriggerCombat()
        {
            // 简单规则：每3回合触发一次战斗
            return Time.frameCount % 90 == 0; // 使用帧数作为简单计时器
        }
        
        bool ShouldExplore()
        {
            // 简单规则：每2回合触发一次探索
            return Time.frameCount % 60 == 0 && _currentPlayer.Mana.GetTotal() >= 2;
        }
        
        void StartCombat()
        {
            _isInCombat = true;
            combatPanel.SetActive(true);
            
            // 创建敌人
            var enemyCard = new UnitCard("goblin", "哥布林", "Goblin", 1, 0, 10, RecruitLocation.Keep, 
                new AttackProfile[] { new AttackProfile(AttackType.Melee, 3, Element.Physical) }, 
                new Ability[] { });
            _currentEnemy = new UnitState(enemyCard);
            Log($"遭遇敌人: 哥布林");
            
            UpdateCombatUI();
        }
        
        void ResolveCombat()
        {
            if (_currentEnemy.Wounds >= _currentEnemy.Card.Level)
            {
                Log("敌人被击败！");
                _isInCombat = false;
                combatPanel.SetActive(false);
            }
            else
            {
                // 敌人反击
                _currentPlayer.Wounds += 2;
                Log($"敌人反击，玩家受到2点伤害");
                
                if (_currentPlayer.Wounds >= _currentPlayer.Armor)
                {
                    Log("玩家被击败！游戏结束");
                    ResetGame();
                }
            }
            
            UpdateCombatUI();
            UpdateUI();
        }
        
        void ExploreMap()
        {
            // 找到可探索的相邻位置
            var neighbors = GetNeighbors(_currentPlayerPosition);
            foreach (var neighbor in neighbors)
            {
                if (!_exploredTiles.ContainsKey(neighbor))
                {
                    ExploreTile(neighbor);
                    break;
                }
            }
        }
        
        void ExploreTile(AxialCoord coord)
        {
            if (_mapState.Countryside.Count > 0)
            {
                var tile = _mapState.Countryside.Draw();
                _exploredTiles[coord] = tile;
                
                // 更新地图显示
                if (_hexObjects.TryGetValue(coord, out var hexObj))
                {
                    var renderer = hexObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = GetTerrainColor(tile.Edges[0]);
                    }
                }
                
                // 移动玩家
                _currentPlayerPosition = coord;
                _currentPlayer.Position = coord;
                MovePlayerTo(coord);
                
                Log($"探索到新地块: {tile.Edges[0]}");
                
                // 假设第一个边缘地形为地点类型
                if (tile.Edges[0] == TerrainType.Mountain || tile.Edges[0] == TerrainType.Forest)
                {
                    Log("发现地点！");
                }
            }
        }
        
        void MovePlayerTo(AxialCoord coord)
        {
            var targetPos = HexToWorldPosition(coord) + Vector3.up * 0.2f;
            var playerToken = GameObject.Find("PlayerToken");
            if (playerToken != null)
            {
                playerToken.transform.position = targetPos;
            }
        }
        
        void UpdateHandDisplay()
        {
            // 清理现有显示
            foreach (Transform child in handContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 创建手牌显示
            for (int i = 0; i < _currentPlayer.Deck.Hand.Count; i++)
            {
                var card = _currentPlayer.Deck.Hand[i];
                var cardObj = Instantiate(cardDisplayPrefab, handContainer);
                var cardUI = cardObj.GetComponent<CardDisplayUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(card);
                }
            }
        }
        
        void UpdateCombatUI()
        {
            if (_isInCombat && _currentEnemy != null)
            {
                playerHealthSlider.maxValue = _currentPlayer.Armor;
                playerHealthSlider.value = _currentPlayer.Armor - _currentPlayer.Wounds;
                
                enemyHealthSlider.maxValue = _currentEnemy.Card.Level;
                enemyHealthSlider.value = _currentEnemy.Card.Level - _currentEnemy.Wounds;
                
                combatLogText.text = $"战斗中...\n玩家血量: {_currentPlayer.Armor - _currentPlayer.Wounds}\n敌人血量: {_currentEnemy.Card.Level - _currentEnemy.Wounds}";
            }
        }
        
        void UpdateUI()
        {
            // 更新游戏状态
            gameStateText.text = $"游戏状态:\n" +
                               $"回合: {Time.frameCount / 30}\n" +
                               $"玩家血量: {_currentPlayer.Armor - _currentPlayer.Wounds}\n" +
                               $"魔力: {_currentPlayer.Mana.GetTotal()}\n" +
                               $"手牌: {_currentPlayer.Deck.Hand.Count}\n" +
                               $"牌库: {_currentPlayer.Deck.DrawPileCount}\n" +
                               $"探索地块: {_exploredTiles.Count}";
            
            // 更新回合信息
            turnInfoText.text = _isInCombat ? "战斗阶段" : "探索阶段";
            
            // 更新玩家状态
            playerStatusText.text = $"玩家状态:\n" +
                                   $"位置: ({_currentPlayerPosition.Q}, {_currentPlayerPosition.R})\n" +
                                   $"状态: {(_isInCombat ? "战斗中" : "探索中")}";
            
            UpdateCombatUI();
        }
        
        void ResetGame()
        {
            _players = new List<PlayerState>();
            _gameEngine = new GameEngine(_players, 6);
            _exploredTiles.Clear();
            _currentPlayerPosition = new AxialCoord(0, 0);
            _isInCombat = false;
            
            InitializeGame();
            
            // 重置地图显示
            foreach (var hexObj in _hexObjects.Values)
            {
                var renderer = hexObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.gray;
                }
            }
            
            MovePlayerTo(_currentPlayerPosition);
            UpdateUI();
            
            Log("游戏已重置");
        }
        
        List<AxialCoord> GetNeighbors(AxialCoord coord)
        {
            var neighbors = new List<AxialCoord>();
            var directions = new AxialCoord[]
            {
                new AxialCoord(1, 0), new AxialCoord(1, -1), new AxialCoord(0, -1),
                new AxialCoord(-1, 0), new AxialCoord(-1, 1), new AxialCoord(0, 1)
            };
            
            foreach (var dir in directions)
            {
                neighbors.Add(new AxialCoord(coord.Q + dir.Q, coord.R + dir.R));
            }
            
            return neighbors;
        }
        
        Vector3 HexToWorldPosition(AxialCoord coord)
        {
            float x = hexSize * (3f/2f * coord.Q);
            float z = hexSize * (Mathf.Sqrt(3)/2 * coord.Q + Mathf.Sqrt(3) * coord.R);
            return new Vector3(x, 0, z);
        }
        
        Color GetTerrainColor(TerrainType type)
        {
            return type switch
            {
                TerrainType.Plains => Color.green,
                TerrainType.Hills => new Color(0.5f, 0.3f, 0.1f),
                TerrainType.Forest => Color.green * 0.7f,
                TerrainType.Desert => Color.yellow,
                TerrainType.Swamp => Color.magenta,
                TerrainType.Mountain => Color.gray,
                _ => Color.white
            };
        }
        
        void ToggleDebugPanel()
        {
            debugPanel.SetActive(!debugPanel.activeSelf);
        }
        
        void Log(string message)
        {
            _gameLog.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log(message);
            
            if (debugLogText != null)
            {
                var recentLogs = _gameLog.Count > 10 ? _gameLog.Skip(_gameLog.Count - 10).ToArray() : _gameLog.ToArray();
                debugLogText.text = string.Join("\n", recentLogs);
            }
        }
    }
    
    public class CardDisplayUI : MonoBehaviour
    {
        public Text cardNameText;
        public Text cardDescriptionText;
        public Image cardBackground;
        
        public void Initialize(DeedCard card)
        {
            if (cardNameText != null)
                cardNameText.text = card.Id;
            
            if (cardDescriptionText != null)
                cardDescriptionText.text = card.Type.ToString();
            
            if (cardBackground != null)
            {
                cardBackground.color = GetCardColor(card);
            }
        }
        
        Color GetCardColor(DeedCard card)
        {
            return card.Type switch
            {
                CardType.Spell => Color.blue,
                CardType.Action => Color.green,
                CardType.Artifact => Color.yellow,
                _ => Color.white
            };
        }
    }
}