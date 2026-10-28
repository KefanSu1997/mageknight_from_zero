using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using Newtonsoft.Json.Linq;

namespace MK.Logic.Runtime.Map
{
    // Definitions are private environment data until revealed. This is deliberately
    // separate from OriginalMapCatalog, whose contract is an already placed opening.
    public sealed class PrintedTileCell
    {
        public AxialCoord Local { get; }
        public string Terrain { get; }
        public string Site { get; }
        public string RuleId { get; }
        public string ManaColor { get; }
        public string CityColor { get; }
        public string EnemyTokenColor { get; }
        internal PrintedTileCell(AxialCoord local, string terrain, string site,
            string mana, string city, string enemy, string ruleId)
        { Local = local; Terrain = terrain; Site = site; ManaColor = mana; CityColor = city; EnemyTokenColor = enemy; RuleId = ruleId; }
    }

    public sealed class PrintedTileDefinition
    {
        public string Id { get; }
        public string Kind { get; }
        public IReadOnlyList<PrintedTileCell> Cells { get; }
        public bool HasCity => Cells.Any(c => c.Site == "City");
        // Both city and non-city core tiles have brown backs. Do not reveal city identity here.
        public string DeckBack => Kind == "Starting" ? "Starting" : Kind == "Countryside" ? "Countryside" : "Core";
        internal PrintedTileDefinition(string id, string kind, List<PrintedTileCell> cells)
        { Id = id; Kind = kind; Cells = cells.AsReadOnly(); }
    }

    public sealed class PrintedTileCatalog
    {
        public IReadOnlyDictionary<string, PrintedTileDefinition> Tiles { get; }
        private PrintedTileCatalog(Dictionary<string, PrintedTileDefinition> tiles)
        { Tiles = new ReadOnlyDictionary<string, PrintedTileDefinition>(tiles); }
        private static readonly HashSet<string> Sites = new HashSet<string> {
            "Portal", "Village", "MagicalGlade", "CrystalMine", "RampagingOrcs", "RampagingDragon",
            "Keep", "MageTower", "Monastery", "Dungeon", "Tomb", "MonsterDen", "SpawningGround", "AncientRuin", "City"
        };
        private static readonly HashSet<string> Colors = new HashSet<string> { "Red", "Blue", "Green", "White" };
        private static string Text(JToken row, string key) => row[key]?.Type == JTokenType.String
            ? (string)row[key] : throw new InvalidDataException("Missing string " + key);
        private static int Int(JToken row, string key) => row[key]?.Type == JTokenType.Integer
            ? (int)row[key] : throw new InvalidDataException("Missing integer " + key);

        public static PrintedTileCatalog Load(params string[] paths) => Parse(paths.Select(File.ReadAllText).ToArray());
        public static PrintedTileCatalog Parse(params string[] documents)
        {
            var result = new Dictionary<string, PrintedTileDefinition>();
            var shape = new HashSet<AxialCoord>(AxialCoord.NeighborDirs) { new AxialCoord(0, 0) };
            foreach (var document in documents)
            {
                var root = JObject.Parse(document);
                if ((int?)root["schemaVersion"] != 1 || (string)root["coordinates"]?["type"] != "axial"
                    || (bool?)root["coordinates"]?["rotationAllowed"] != false)
                    throw new InvalidDataException("Unsupported printed tile schema/orientation");
                var definitions = root["tiles"] as JArray ?? throw new InvalidDataException("Missing tiles");
                foreach (var tile in definitions)
                {
                    string id = Text(tile, "id"), kind = Text(tile, "kind");
                    if (Text(tile, "orientation") != "printed_upright" || tile["rotation"] != null
                        || (kind != "Starting" && kind != "Countryside" && kind != "Core"))
                        throw new InvalidDataException("Invalid printed tile kind/orientation");
                    // Expansion walls require explicit support, never silently discard them.
                    if (tile["walls"] != null) throw new InvalidDataException("Wall definitions not yet supported");
                    var cells = new List<PrintedTileCell>(); var seen = new HashSet<AxialCoord>();
                    foreach (var cell in tile["cells"] as JArray ?? throw new InvalidDataException("Missing cells"))
                    {
                        var local = new AxialCoord(Int(cell, "q"), Int(cell, "r"));
                        string terrain = Text(cell, "terrain"), site = (string)cell["site"],
                            mana = (string)cell["manaColor"], city = (string)cell["cityColor"], enemy = (string)cell["enemyTokenColor"];
                        if (!shape.Contains(local) || !seen.Add(local)) throw new InvalidDataException("Invalid seven-hex shape");
                        if (terrain != "Sea" && (!Enum.TryParse<TerrainType>(terrain, out var parsed)
                            || !Enum.IsDefined(typeof(TerrainType), parsed))) throw new InvalidDataException("Unknown terrain");
                        if (site != null && (!Sites.Contains(site) || terrain == "Sea")) throw new InvalidDataException("Unknown or sea site");
                        if ((site == "CrystalMine") != (mana != null) || mana != null && !Colors.Contains(mana))
                            throw new InvalidDataException("Invalid mine color");
                        if ((site == "City") != (city != null) || city != null && (!Colors.Contains(city) || kind != "Core"))
                            throw new InvalidDataException("Invalid city color/kind");
                        string expectedEnemy = site == "RampagingOrcs" ? "Green" : site == "RampagingDragon" ? "Red" : null;
                        if (enemy != expectedEnemy) throw new InvalidDataException("Invalid rampaging token color");
                        if (cell["walls"] != null) throw new InvalidDataException("Wall definitions not yet supported");
                        cells.Add(new PrintedTileCell(local, terrain, site, mana, city, enemy, (string)cell["ruleId"]));
                    }
                    if (!seen.SetEquals(shape) || cells.Count(c => c.Site == "City") > 1)
                        throw new InvalidDataException("Incomplete tile or duplicate city");
                    if (!result.TryAdd(id, new PrintedTileDefinition(id, kind, cells))) throw new InvalidDataException("Duplicate tile ID " + id);
                }
            }
            if (result.Count == 0) throw new InvalidDataException("Empty catalog");
            return new PrintedTileCatalog(result);
        }
    }
}
