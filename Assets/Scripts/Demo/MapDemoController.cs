using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Runtime.Map;
using MK.Logic.Core;
using MK.Logic.Runtime;
using System;

namespace MK.Demo
{
    public class MapDemoController : MonoBehaviour
    {
        [Header("Map Settings")]
        public int mapSize = 10;
        public float hexSize = 1f;
        public Material hexMaterial;
        public Material selectedHexMaterial;
        
        [Header("UI References")]
        public Text mapInfoText;
        public Text tileInfoText;
        public Button exploreButton;
        public Button resetMapButton;
        public Dropdown tileTypeDropdown;
        public Slider rotationSlider;
        
        [Header("Visual Prefabs")]
        public GameObject hexTilePrefab;
        public GameObject playerTokenPrefab;
        
        private MapState _mapState;
        private ExplorationService _explorationService;
        private AxialCoord _selectedHex;
        private AxialCoord _playerPosition = new AxialCoord(0, 0);
        private GameObject _playerToken;
        private Dictionary<AxialCoord, GameObject> _hexObjects = new Dictionary<AxialCoord, GameObject>();
        private Dictionary<AxialCoord, MapTile> _placedTiles = new Dictionary<AxialCoord, MapTile>();
        
        private int _playerMovement = 5;
        private bool _isExploring = false;
        
        void Start()
        {
            InitializeMap();
            SetupUI();
            CreateHexGrid();
            CreatePlayerToken();
            UpdateUI();
        }
        
        void InitializeMap()
        {
            _mapState = new MapState();
            _mapState.Countryside = new TileDeck();
            _mapState.Core = new TileDeck();
            
            // 添加测试地块
            PopulateTileDecks();
            
            _explorationService = new ExplorationService(_mapState, new System.Random(42));
        }
        
        void PopulateTileDecks()
        {
            // 添加乡村地块
            for (int i = 0; i < 10; i++)
            {
                var edges = new TerrainType[6];
                for (int j = 0; j < 6; j++) edges[j] = (TerrainType)UnityEngine.Random.Range(0, 3);
                var tile = new MapTile(TileSet.Countryside, i, edges);
                _mapState.Countryside.Push(tile);
            }
            
            // 添加核心地块
            for (int i = 0; i < 5; i++)
            {
                var edges = new TerrainType[6];
                for (int j = 0; j < 6; j++) edges[j] = (TerrainType)UnityEngine.Random.Range(3, 6);
                var tile = new MapTile(TileSet.Core, i + 100, edges);
                _mapState.Core.Push(tile);
            }
        }
        
        void SetupUI()
        {
            exploreButton.onClick.AddListener(OnExploreClicked);
            resetMapButton.onClick.AddListener(ResetMap);
            
            // 设置下拉菜单
            tileTypeDropdown.ClearOptions();
            tileTypeDropdown.AddOptions(new List<string> { "乡村", "核心", "全部" });
            
            rotationSlider.onValueChanged.AddListener(OnRotationChanged);
        }
        
        void CreateHexGrid()
        {
            for (int q = -mapSize; q <= mapSize; q++)
            {
                for (int r = -mapSize; r <= mapSize; r++)
                {
                    if (Math.Abs(q + r) <= mapSize)
                    {
                        var coord = new AxialCoord(q, r);
                        CreateHexTile(coord);
                    }
                }
            }
        }
        
        void CreateHexTile(AxialCoord coord)
        {
            var hexPos = HexToWorldPosition(coord);
            var hexObj = Instantiate(hexTilePrefab, hexPos, Quaternion.identity);
            hexObj.name = $"Hex_{coord.Q}_{coord.R}";
            
            // 设置六边形大小
            hexObj.transform.localScale = Vector3.one * hexSize;
            
            // 添加点击事件
            var clickable = hexObj.AddComponent<ClickableHex>();
            clickable.Initialize(coord, OnHexClicked);
            
            _hexObjects[coord] = hexObj;
            
            // 初始颜色
            UpdateHexColor(coord);
        }
        
        void CreatePlayerToken()
        {
            var playerPos = HexToWorldPosition(_playerPosition);
            _playerToken = Instantiate(playerTokenPrefab, playerPos, Quaternion.identity);
            _playerToken.name = "PlayerToken";
        }
        
        void OnHexClicked(AxialCoord coord)
        {
            if (_isExploring)
            {
                // 探索模式：检查是否可以探索
                if (IsValidExploreTarget(coord))
                {
                    _selectedHex = coord;
                    HighlightValidHexes();
                    UpdateTileInfo(coord);
                }
            }
            else
            {
                // 选择模式：高亮选中的六边形
                _selectedHex = coord;
                HighlightSelectedHex();
                UpdateTileInfo(coord);
            }
        }
        
        bool IsValidExploreTarget(AxialCoord coord)
        {
            // 检查是否是相邻的未探索位置
            if (_placedTiles.ContainsKey(coord))
                return false;
                
            // 检查是否相邻
            var neighbors = GetNeighbors(_playerPosition);
            return neighbors.Contains(coord);
        }
        
        void OnExploreClicked()
        {
            if (IsValidExploreTarget(_selectedHex))
            {
                int rotation = (int)rotationSlider.value;
                
                var result = _explorationService.Explore(
                    new PlayerState(1, "DemoPlayer"), 
                    _selectedHex, 
                    rotation, 
                    _playerMovement
                );
                
                if (result.Success)
                {
                    PlaceTile(_selectedHex, result.Tile, rotation);
                    _playerMovement = result.RemainingMovement;
                    _playerPosition = _selectedHex;
                    MovePlayerTo(_selectedHex);
                    
                    Log($"探索成功！剩余移动力: {_playerMovement}");
                    Log($"发现地块: {GetTerrainName(result.Tile.Edges[0])}");
                    
                    // 简化处理：假设第一个边缘地形代表地块类型
                    Log("发现新地块！");
                }
                else
                {
                    Log($"探索失败: {result.ErrorMessage}");
                }
                
                UpdateUI();
            }
            else
            {
                Log("请选择有效的探索目标");
            }
        }
        
        void PlaceTile(AxialCoord coord, MapTile tile, int rotation)
        {
            _placedTiles[coord] = tile;
            
            if (_hexObjects.TryGetValue(coord, out var hexObj))
            {
                // 更新地块颜色
                var renderer = hexObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetTerrainColor(tile.Edges[0]);
                }
                
                // 添加旋转
                hexObj.transform.rotation = Quaternion.Euler(0, rotation, 0);
                
                // 添加地点标记
                if (tile.Edges[0] == TerrainType.Mountain || tile.Edges[0] == TerrainType.Forest)
                {
                    var siteMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    siteMarker.transform.position = hexObj.transform.position + Vector3.up * 0.5f;
                    siteMarker.transform.localScale = Vector3.one * 0.3f;
                    siteMarker.GetComponent<Renderer>().material.color = Color.yellow;
                    siteMarker.transform.SetParent(hexObj.transform);
                }
            }
        }
        
        void MovePlayerTo(AxialCoord coord)
        {
            var targetPos = HexToWorldPosition(coord) + Vector3.up * 0.2f;
            StartCoroutine(MovePlayerCoroutine(targetPos));
        }
        
        System.Collections.IEnumerator MovePlayerCoroutine(Vector3 targetPos)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 startPos = _playerToken.transform.position;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _playerToken.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            
            _playerToken.transform.position = targetPos;
        }
        
        void HighlightValidHexes()
        {
            // 重置所有六边形颜色
            foreach (var coord in _hexObjects.Keys)
            {
                UpdateHexColor(coord);
            }
            
            // 高亮可探索的六边形
            var validTargets = GetNeighbors(_playerPosition);
            foreach (var coord in validTargets)
            {
                if (!_placedTiles.ContainsKey(coord) && _hexObjects.TryGetValue(coord, out var hexObj))
                {
                    var renderer = hexObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.green;
                    }
                }
            }
        }
        
        void HighlightSelectedHex()
        {
            // 重置所有六边形颜色
            foreach (var coord in _hexObjects.Keys)
            {
                UpdateHexColor(coord);
            }
            
            // 高亮选中的六边形
            if (_hexObjects.TryGetValue(_selectedHex, out var hexObj))
            {
                var renderer = hexObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.yellow;
                }
            }
        }
        
        void UpdateHexColor(AxialCoord coord)
        {
            if (_hexObjects.TryGetValue(coord, out var hexObj))
            {
                var renderer = hexObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (_placedTiles.TryGetValue(coord, out var tile))
                    {
                        renderer.material.color = GetTerrainColor(tile.Edges[0]);
                    }
                    else
                    {
                        renderer.material.color = Color.gray;
                    }
                }
            }
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
        
        void UpdateTileInfo(AxialCoord coord)
        {
            string info = $"坐标: ({coord.Q}, {coord.R})";
            
            if (_placedTiles.TryGetValue(coord, out var tile))
            {
                info += $"\n地形: {GetTerrainName(tile.Edges[0])}";
                info += $"\n地点: {(tile.Edges[0] == TerrainType.Mountain || tile.Edges[0] == TerrainType.Forest ? "有" : "无")}";
            }
            else
            {
                info += "\n状态: 未探索";
            }
            
            tileInfoText.text = info;
        }
        
        void UpdateUI()
        {
            mapInfoText.text = $"地图信息:\n" +
                              $"已探索地块: {_placedTiles.Count}\n" +
                              $"剩余移动力: {_playerMovement}\n" +
                              $"乡村地块: {_mapState.Countryside.Count}\n" +
                              $"核心地块: {_mapState.Core.Count}";
        }
        
        void OnRotationChanged(float rotation)
        {
            // 更新旋转预览
            if (_hexObjects.TryGetValue(_selectedHex, out var hexObj))
            {
                hexObj.transform.rotation = Quaternion.Euler(0, rotation, 0);
            }
        }
        
        void ResetMap()
        {
            // 清理已放置的地块
            _placedTiles.Clear();
            _playerPosition = new AxialCoord(0, 0);
            _playerMovement = 5;
            
            // 重新初始化地图
            InitializeMap();
            
            // 重置六边形颜色
            foreach (var coord in _hexObjects.Keys)
            {
                UpdateHexColor(coord);
            }
            
            // 重置玩家位置
            _playerToken.transform.position = HexToWorldPosition(_playerPosition) + Vector3.up * 0.2f;
            
            UpdateUI();
            Log("地图已重置");
        }
        
        string GetTerrainName(TerrainType type)
        {
            return type switch
            {
                TerrainType.Plains => "平原",
                TerrainType.Hills => "丘陵",
                TerrainType.Forest => "森林",
                TerrainType.Desert => "沙漠",
                TerrainType.Swamp => "沼泽",
                TerrainType.Mountain => "山脉",
                _ => "未知"
            };
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
        
        void Log(string message)
        {
            Debug.Log($"[MapDemo] {message}");
        }
    }
    
    public class ClickableHex : MonoBehaviour
    {
        private AxialCoord _coord;
        private System.Action<AxialCoord> _onClick;
        
        public void Initialize(AxialCoord coord, System.Action<AxialCoord> onClick)
        {
            _coord = coord;
            _onClick = onClick;
        }
        
        void OnMouseDown()
        {
            _onClick?.Invoke(_coord);
        }
    }
}