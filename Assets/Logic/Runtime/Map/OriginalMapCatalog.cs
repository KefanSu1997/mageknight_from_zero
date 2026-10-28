using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using Newtonsoft.Json.Linq;

namespace MK.Logic.Runtime.Map
{
    public sealed class OriginalMapCell
    {
        public string TileId { get; internal set; }
        public AxialCoord Local { get; internal set; }
        public AxialCoord Position { get; internal set; }
        public string TerrainName { get; internal set; }
        public TerrainType? Terrain { get; internal set; }
        public bool IsSea => TerrainName == "Sea";
        public string Site { get; internal set; }
        public string RuleId { get; internal set; }
        public ManaColor? ManaColor { get; internal set; }
        public EnemyTokenColor? EnemyTokenColor { get; internal set; }
        public CityColor? CityColor { get; internal set; }
    }

    public sealed class OriginalMapTile
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public string Orientation { get; internal set; }
        public AxialCoord Anchor { get; internal set; }
        public IReadOnlyList<OriginalMapCell> Cells { get; internal set; }
    }

    /// <summary>Loads the verified opening only. It does not create an exploration supply or claim scenario completion.</summary>
    public sealed class OriginalMapCatalog
    {
        public IReadOnlyList<OriginalMapTile> Tiles { get; private set; }
        public string SourcePath { get; private set; }
        public string SourceSha256 { get; private set; }
        public static OriginalMapCatalog Load(string path) => Parse(File.ReadAllText(path));
        private static string Required(JToken token, string key)
            => (string)token[key] ?? throw new InvalidDataException("原地图缺少字段：" + key);
        private static int Integer(JToken token, string key)
            => token[key]?.Type == JTokenType.Integer ? (int)token[key] : throw new InvalidDataException("原地图坐标必须为整数：" + key);

        public static OriginalMapCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            if ((int?)root["schemaVersion"] != 1 || (string)root["coordinates"]?["type"] != "axial"
                || (bool?)root["coordinates"]?["rotationAllowed"] != false)
                throw new InvalidDataException("原地图必须使用已核对的轴向坐标及固定朝向");
            var definitions = root["tiles"] as JArray ?? throw new InvalidDataException("缺少原板块定义");
            var placements = root["initialPlacement"] as JArray ?? throw new InvalidDataException("缺少原起始布局");
            var byId = new Dictionary<string, JToken>();
            foreach (var definition in definitions)
                if (!byId.TryAdd(Required(definition, "id"), definition)) throw new InvalidDataException("原板块编号重复");
            if (placements.Count == 0 || placements.Count != byId.Count) throw new InvalidDataException("起始布局必须恰好放置每块已定义板块");
            var expected = AxialCoord.NeighborDirs.Concat(new[] { new AxialCoord(0, 0) }).ToHashSet();
            var usedIds = new HashSet<string>(); var positions = new HashSet<AxialCoord>(); var tiles = new List<OriginalMapTile>();
            foreach (var placement in placements)
            {
                string id = Required(placement, "tileId");
                if (!byId.TryGetValue(id, out var definition) || !usedIds.Add(id)) throw new InvalidDataException("原板块放置缺失或重复");
                string orientation = Required(definition, "orientation"), kind = Required(definition, "kind");
                if (orientation != "printed_upright" || placement["rotation"] != null || kind != "Starting" && kind != "Countryside")
                    throw new InvalidDataException("本目录仅接入固定印刷朝向的起始/乡村板块");
                if (kind != "Starting" && (!int.TryParse(id, out int numericId) || numericId < 0)) throw new InvalidDataException("乡村板块需要原编号");
                var anchor = new AxialCoord(Integer(placement, "anchorQ"), Integer(placement, "anchorR"));
                var rows = definition["cells"] as JArray ?? throw new InvalidDataException("缺少原七格");
                var localSeen = new HashSet<AxialCoord>(); var cells = new List<OriginalMapCell>();
                foreach (var row in rows)
                {
                    var local = new AxialCoord(Integer(row, "q"), Integer(row, "r")); var position = anchor + local;
                    if (!expected.Contains(local) || !localSeen.Add(local) || !positions.Add(position)) throw new InvalidDataException("原七格形状错误或板块重叠");
                    string terrainName = Required(row, "terrain"); TerrainType? terrain = null;
                    if (terrainName != "Sea")
                    {
                        if (!Enum.TryParse<TerrainType>(terrainName, out var value) || !Enum.IsDefined(typeof(TerrainType), value)) throw new InvalidDataException("未知原地形");
                        terrain = value;
                    }
                    var cell = new OriginalMapCell { TileId = id, Local = local, Position = position, TerrainName = terrainName,
                        Terrain = terrain, Site = (string)row["site"], RuleId = (string)row["ruleId"] };
                    if (cell.Site != null && cell.Site != "Portal" && cell.Site != "Village" && cell.Site != "MagicalGlade" && cell.Site != "CrystalMine" && cell.Site != "RampagingOrcs")
                        throw new InvalidDataException("此原地点尚未接入起始目录");
                    if (cell.IsSea && cell.Site != null) throw new InvalidDataException("海域不能放置地点");
                    if (cell.Site == "CrystalMine")
                    {
                        if ((string)row["manaColor"] != "Green") throw new InvalidDataException("此目录只包含已核对的绿矿");
                        cell.ManaColor = MK.Logic.Core.ManaColor.Green;
                    }
                    if (cell.Site == "RampagingOrcs")
                    {
                        if ((string)row["enemyTokenColor"] != "Green") throw new InvalidDataException("肆虐兽人必须取真实绿色标记");
                        cell.EnemyTokenColor = MK.Logic.Core.EnemyTokenColor.Green;
                    }
                    cells.Add(cell);
                }
                if (!localSeen.SetEquals(expected)) throw new InvalidDataException("每块原板块必须包含中心与六邻格");
                tiles.Add(new OriginalMapTile { Id = id, Kind = kind, Orientation = orientation, Anchor = anchor, Cells = cells.AsReadOnly() });
            }
            return new OriginalMapCatalog { Tiles = tiles.AsReadOnly(), SourcePath = (string)root["source"]?["path"], SourceSha256 = (string)root["source"]?["sha256"] };
        }

        public void Initialize(MapState map, EnemyTokenSupply supply)
        {
            if (map == null || supply == null) throw new ArgumentNullException(map == null ? nameof(map) : nameof(supply));
            if (map.Placed.Count != 0 || map.Places.Count != 0 || map.RevealedSites.Count != 0 || map.OriginalTiles.Count != 0 || map.OriginalCells.Count != 0
                || map.Countryside.Count != 0 || map.Core.Count != 0 || map.CityTile != null)
                throw new InvalidOperationException("原起始布局仅能初始化全新空地图");
            int greenNeeded = Tiles.SelectMany(t => t.Cells).Count(c => c.EnemyTokenColor == MK.Logic.Core.EnemyTokenColor.Green);
            if (supply.DrawPile.Concat(supply.DiscardPile).Count(t => t.TokenColor == MK.Logic.Core.EnemyTokenColor.Green) < greenNeeded)
                throw new InvalidOperationException("起始肆虐兽人的真实绿标记不足，未修改地图或供给");
            foreach (var tile in Tiles)
            {
                map.OriginalTiles.Add(tile.Id, tile);
                foreach (var cell in tile.Cells)
                {
                    map.OriginalCells.Add(cell.Position, cell);
                    if (cell.IsSea) continue;
                    map.Placed.Add(cell.Position, new MapTile(TileSet.Countryside, tile.Kind == "Starting" ? -1 : int.Parse(tile.Id), new[] { cell.Terrain.Value }));
                    PlaceState place = cell.Site switch
                    {
                        "Village" => new PlaceState(PlaceType.Village),
                        "MagicalGlade" => new PlaceState(PlaceType.MagicalGlade),
                        "CrystalMine" => new PlaceState(PlaceType.ManaMine) { MineColors = new[] { cell.ManaColor.Value } },
                        "RampagingOrcs" => new PlaceState(PlaceType.OrcRampaging) { Enemy = supply.Draw(MK.Logic.Core.EnemyTokenColor.Green) },
                        _ => null
                    };
                    if (place == null) continue;
                    map.Places.Add(cell.Position, place);
                    if (place.Enemy != null) map.RevealedSites.Add(place);
                }
            }
        }
    }
}
