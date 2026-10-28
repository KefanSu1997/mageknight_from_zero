using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Map
{
    /// <summary>
    /// Commits a seven-cell printed tile and already prepared real site instances.
    /// Site supply draws/rewards and movement payment belong to the caller's reveal transaction.
    /// All map validation precedes mutation; omitted or aliased sites are rejected.
    /// </summary>
    public static class OriginalTileMapPlacement
    {
        public static PlaceType SiteType(string site) => site switch
        {
            "Village" => PlaceType.Village, "Monastery" => PlaceType.Monastery,
            "CrystalMine" => PlaceType.ManaMine, "MagicalGlade" => PlaceType.MagicalGlade,
            "RampagingOrcs" => PlaceType.OrcRampaging, "RampagingDragon" => PlaceType.Dragon,
            "Keep" => PlaceType.Keep, "MageTower" => PlaceType.MageTower,
            "Dungeon" => PlaceType.Dungeon, "Tomb" => PlaceType.Tomb,
            "MonsterDen" => PlaceType.MonsterDen, "SpawningGround" => PlaceType.SpawningGround,
            "AncientRuin" => PlaceType.AncientRuin, "City" => PlaceType.City,
            _ => throw new ArgumentException("Not a native site: " + site)
        };

        public static void Place(MapState map, PrintedTileDefinition definition, AxialCoord anchor,
            IReadOnlyDictionary<AxialCoord, PlaceState> preparedLocalSites)
        {
            if (map == null || definition == null || preparedLocalSites == null) throw new ArgumentNullException();
            if (map.OriginalTiles.ContainsKey(definition.Id)) throw new InvalidOperationException("Tile already placed");
            var expected = definition.Cells.Where(c => c.Site != null && c.Site != "Portal").ToArray();
            if (preparedLocalSites.Count != expected.Length || expected.Any(c => !preparedLocalSites.ContainsKey(c.Local)))
                throw new InvalidOperationException("Every printed site must be explicitly prepared");
            var instances = preparedLocalSites.Values.ToArray();
            if (instances.Any(s => s == null) || instances.Distinct().Count() != instances.Length
                || instances.Any(s => map.Places.Values.Contains(s)))
                throw new InvalidOperationException("Sites must be distinct new instances");
            var cells = new List<OriginalMapCell>();
            foreach (var cell in definition.Cells)
            {
                var position = anchor + cell.Local;
                if (map.OriginalCells.ContainsKey(position) || map.Placed.ContainsKey(position) || map.Places.ContainsKey(position))
                    throw new InvalidOperationException("Printed tile overlaps existing map");
                if (cell.Site != null && cell.Site != "Portal")
                {
                    var site = preparedLocalSites[cell.Local];
                    if (site.Type != SiteType(cell.Site)) throw new InvalidOperationException("Printed/native site mismatch");
                    if (cell.Site == "City" && site.CityColor.ToString() != cell.CityColor)
                        throw new InvalidOperationException("City color mismatch");
                    if (cell.Site == "CrystalMine" && (site.MineColors == null || site.MineColors.Length != 1
                        || site.MineColors[0].ToString() != cell.ManaColor)) throw new InvalidOperationException("Mine color mismatch");
                }
                cells.Add(new OriginalMapCell { TileId=definition.Id, Local=cell.Local, Position=position,
                    TerrainName=cell.Terrain, Terrain=cell.Terrain=="Sea" ? null : Enum.Parse<TerrainType>(cell.Terrain),
                    Site=cell.Site, RuleId=cell.RuleId, ManaColor=cell.ManaColor==null ? null : Enum.Parse<ManaColor>(cell.ManaColor),
                    EnemyTokenColor=cell.EnemyTokenColor==null ? null : Enum.Parse<EnemyTokenColor>(cell.EnemyTokenColor),
                    CityColor=cell.CityColor==null ? null : Enum.Parse<CityColor>(cell.CityColor) });
            }
            var tile = new OriginalMapTile { Id=definition.Id,Kind=definition.Kind,Orientation="printed_upright",
                Anchor=anchor,Cells=cells.AsReadOnly() };
            // MapTile is a compatibility terrain projection, not a second source of identity.
            int printedNumber = definition.Kind=="Starting" ? -1 : int.Parse(definition.Id.Replace("core_", ""));
            var terrainTiles=cells.Where(c=>!c.IsSea).ToDictionary(c=>c.Position,
                c=>new MapTile(definition.Kind=="Core" ? TileSet.Core : TileSet.Countryside,printedNumber,new[]{c.Terrain.Value}));
            map.OriginalTiles.Add(tile.Id,tile);
            foreach(var cell in cells)map.OriginalCells.Add(cell.Position,cell);
            foreach(var pair in terrainTiles)map.Placed.Add(pair.Key,pair.Value);
            foreach(var pair in preparedLocalSites)
            {
                map.Places.Add(anchor+pair.Key,pair.Value);
                if(pair.Value.Type==PlaceType.OrcRampaging || pair.Value.Type==PlaceType.Dragon)
                    map.RevealedSites.Add(pair.Value);
            }
        }
    }
}
