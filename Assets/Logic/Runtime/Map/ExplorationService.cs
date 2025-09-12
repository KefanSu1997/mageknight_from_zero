using MK.Logic.Core;
using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// 提供完整的探索流程：支付移动力、抽取、旋转、放置新地块并触发事件。
    /// </summary>
    public sealed class ExplorationService
    {
        private readonly MapState _map;
        private readonly TilePlacementService _placer = new();
        private readonly MonsterSpawningService _monsterSpawner = new();
        private readonly Random _random;

        public ExplorationService(MapState map, Random? random = null)
        {
            _map = map;
            _random = random ?? new Random();
        }

        /// <summary>
        /// 探索新地块的完整流程
        /// </summary>
        /// <param name="explorer">执行探索的玩家</param>
        /// <param name="coord">新地块放置坐标</param>
        /// <param name="rotation">地块旋转角度 (0, 60, 120, 180, 240, 300)</param>
        /// <param name="movementPoints">可用的移动力点数</param>
        /// <returns>探索结果，包括新地块和其他生成元素</returns>
        public ExplorationResult Explore(PlayerState explorer, AxialCoord coord, int rotation, int movementPoints)
        {
            // Step 1: 检查探索是否可行
            var validationResult = ValidateExploration(explorer, coord, movementPoints);
            if (!validationResult.IsValid)
                return ExplorationResult.FromError(validationResult.ErrorMessage!);

            // Step 2: 支付移动力 (探索需要2点移动力)
            int explorationCost = 2;
            int remainingMovement = movementPoints - explorationCost;
            
            // Step 3: 从相应的牌堆抽取新地块
            MapTile tile = DrawNewTile();
            
            // Step 4: 应用旋转
            if (rotation != 0)
            {
                tile.Rotate(rotation / 60);
            }
            
            // Step 5: 放置新地块
            _placer.Place(_map, tile, coord, rotation);
            
            // 注意：这里需要一个临时的MapTile来处理验证
            var tempTile = tile;
            
            // Step 6: 根据规则放置敌人或建筑
            var spawnedElements = ProcessTileEvents(tile, coord);
            
            // Step 7: 如果发现威胁，触发战斗
            if (spawnedElements.HasThreat)
            {
                return ExplorationResult.FromCombat(tile, spawnedElements.GetCombat(), remainingMovement, coord);
            }
            
            return ExplorationResult.FromSuccess(tile, spawnedElements.GetElements(), remainingMovement, coord);
        }

        /// <summary>
        /// 验证探索条件
        /// </summary>
        private ValidationResult ValidateExploration(PlayerState explorer, AxialCoord coord, int availableMovement)
        {
            if (availableMovement < 2)
                return ValidationResult.Invalid("探索需要2点移动力");
                
            // 使用空白tile验证放置可行性
            var mockTile = new MapTile(TileSet.Countryside, 0, new TerrainType[6]);
            if (!_placer.CanPlace(_map, mockTile, coord, 0))
                return ValidationResult.Invalid("指定的坐标无法放置新地块");
                
            return ValidationResult.Valid();
        }

        /// <summary>
        /// 从对应牌堆抽取新地块
        /// </summary>
        private MapTile DrawNewTile()
        {
            // 优先抽郊外牌堆，如果没有则抽核心牌堆
            if (_map.Countryside.Count > 0)
                return _map.Countryside.Draw();
            else
                return _map.Core.Draw();
        }

        /// <summary>
        /// 处理地块放置后的事件，包括敌人生成
        /// </summary>
        private SpawnResult ProcessTileEvents(MapTile tile, AxialCoord coord)
        {
            var spawnedElements = new List<SpawnedElement>();
            
            // 简化的处理 - 根据地形类型生成相应元素
            // 实际应用中需要根据tile的实际内容来判断
            if (_random.Next(100) < 30) // 30% 概率
            {
                var monster = _monsterSpawner.SpawnRandomMonster(coord);
                spawnedElements.Add(new SpawnedElement("Monster", monster, coord));
                return SpawnResult.WithThreat(spawnedElements);
            }

            return SpawnResult.FromElements(spawnedElements);
        }
    }

    /// <summary>
    /// 探索结果数据结构
    /// </summary>
    public class ExplorationResult
    {
        public bool Success { get; }
        public MapTile? Tile { get; }
        public string? ErrorMessage { get; }
        public int RemainingMovement { get; }
        public AxialCoord NewTileCoord { get; }
        public IReadOnlyList<SpawnedElement>? Elements { get; }
        public Combat? CombatEvent { get; }

        private ExplorationResult(bool success, MapTile? tile, string? error, int remainingMovement, 
            AxialCoord coord, IReadOnlyList<SpawnedElement>? elements, Combat? combat)
        {
            Success = success;
            Tile = tile;
            ErrorMessage = error;
            RemainingMovement = remainingMovement;
            NewTileCoord = coord;
            Elements = elements;
            CombatEvent = combat;
        }

        public static ExplorationResult FromSuccess(MapTile tile, IReadOnlyList<SpawnedElement> elements, 
            int remainingMovement, AxialCoord coord)
            => new(true, tile, null, remainingMovement, coord, elements, null);
            
        public static ExplorationResult FromError(string error)
            => new(false, null, error, 0, default(AxialCoord), null, null);
            
        public static ExplorationResult FromCombat(MapTile tile, Combat combat, int remainingMovement, AxialCoord coord)
            => new(true, tile, null, remainingMovement, coord, null, combat);
    }

    /// <summary>
    /// 生成元素数据结构
    /// </summary>
    public record SpawnedElement(string Type, object Data, AxialCoord Position);
    
    /// <summary>
    /// 战斗触发事件
    /// </summary>
    public record Combat(Monster enemy, AxialCoord tileCoord);
    
    /// <summary>
    /// 验证结果
    /// </summary>
    public record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Valid() => new(true, null);
        public static ValidationResult Invalid(string error) => new(false, error);
    }
    
    /// <summary>
    /// 生成结果内部类
    /// </summary>
    internal record SpawnResult(bool HasThreat, List<SpawnedElement> SpElements, Combat? SpCombat)
    {
        public static SpawnResult FromElements(List<SpawnedElement> elements) => new(false, elements, null);
        public static SpawnResult WithThreat(List<SpawnedElement> elements) => new(true, elements, null);
        public Combat GetCombat() => SpCombat ?? throw new InvalidOperationException("No combat available");
        public List<SpawnedElement> GetElements() => SpElements;
    }
}
