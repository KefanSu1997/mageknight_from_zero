using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MK.Tests.Map
{
    public sealed class OriginalMapOpeningTests
    {
        private string Source => Path.Combine(Directory.GetCurrentDirectory(), "resources/original_maps/tutorial_opening.json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static EnemyTokenSupply Supply(int count = 2) => new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json/monster.json"))).Where(m => m.TokenColor == EnemyTokenColor.Green).Take(count), new Random(0));

        [Test] public void OriginalThreeTilesPreserveTwentyOneCellsSitesAndActualPublicGreenTokens()
        {
            var catalog = OriginalMapCatalog.Load(Source); var map = new MapState(); var supply = Supply(); var original = supply.DrawPile.ToArray();
            catalog.Initialize(map, supply);
            Assert.That(map.OriginalTiles.Keys, Is.EquivalentTo(new[] { "A", "1", "2" }));
            Assert.That(map.OriginalCells.Count, Is.EqualTo(21)); Assert.That(map.Placed.Count, Is.EqualTo(18));
            Assert.That(map.OriginalTiles.Values.All(t => t.Cells.Count == 7 && t.Orientation == "printed_upright"), Is.True);
            Assert.That(map.OriginalTiles["1"].Anchor, Is.EqualTo(new AxialCoord(1, -3)));
            Assert.That(map.OriginalTiles["2"].Anchor, Is.EqualTo(new AxialCoord(3, -2)));
            foreach (var tile in map.OriginalTiles.Values)
            foreach (var cell in tile.Cells)
            {
                Assert.That(cell.Position, Is.EqualTo(tile.Anchor + cell.Local)); Assert.That(cell.TileId, Is.EqualTo(tile.Id));
                Assert.That(map.OriginalCells[cell.Position], Is.SameAs(cell));
                if (cell.IsSea) Assert.That(map.Placed.ContainsKey(cell.Position), Is.False);
                else Assert.That(map.Placed[cell.Position].Edges.Single(), Is.EqualTo(cell.Terrain.Value));
            }
            Assert.That(map.OriginalCells[new AxialCoord(0, 0)].Site, Is.EqualTo("Portal")); Assert.That(map.Places.ContainsKey(new AxialCoord(0, 0)), Is.False);
            Assert.That(map.Places.Values.Count(p => p.Type == PlaceType.Village), Is.EqualTo(2));
            Assert.That(map.Places.Values.Count(p => p.Type == PlaceType.MagicalGlade), Is.EqualTo(2));
            var mine = map.Places.Values.Single(p => p.Type == PlaceType.ManaMine); Assert.That(mine.MineColors, Is.EqualTo(new[] { ManaColor.Green }));
            var guards = map.Places.Values.Where(p => p.Type == PlaceType.OrcRampaging).ToArray(); Assert.That(guards.Length, Is.EqualTo(2));
            Assert.That(guards[0].Enemy, Is.SameAs(original[0])); Assert.That(guards[1].Enemy, Is.SameAs(original[1]));
            Assert.That(guards.All(p => map.RevealedSites.Contains(p) && supply.IsInUse(p.Enemy)), Is.True);
            Assert.That(supply.DrawPile, Is.Empty); Assert.That(supply.DiscardPile, Is.Empty); Assert.That(supply.InUse.Count, Is.EqualTo(2));
        }

        [Test] public void OriginalCardsPayActualAdjacentTerrainAndSeaNeverBecomesLake()
        {
            var map = new MapState(); OriginalMapCatalog.Load(Source).Initialize(map, Supply());
            var p = new PlayerState(); var c = new ActionContext { DayPart = DayPart.Day, MovementMap = map };
            var definition = CardJsonLoader.LoadBasicActions().Single(a => a.Id == "basic_card_000");
            var march = new DeedCard(definition.Id, CardType.Action); p.Deck.Hand.Add(march); p.Mana.AddToken(ManaColor.Green);
            CardPlayActions.Play(p, c, march, definition, true); Assert.That(c.MovementPool, Is.EqualTo(4));
            var move = new MovementService(); var sea = new AxialCoord(0, 1);
            c.TerrainCostOverride[TerrainType.Lake] = 0; c.LakeSafe = true;
            Assert.That(move.TryMoveUsingPool(p, sea, map, c), Is.False); Assert.That(c.MovementPool, Is.EqualTo(4)); Assert.That(p.Position, Is.EqualTo(new AxialCoord(0, 0)));
            Assert.That(move.TryMoveUsingPool(p, new AxialCoord(1, 0), map, c), Is.True); Assert.That(c.MovementPool, Is.EqualTo(2));
            Assert.That(move.TryMoveUsingPool(p, new AxialCoord(1, -1), map, c), Is.False); Assert.That(c.MovementPool, Is.EqualTo(2));
            var second = new DeedCard(definition.Id, CardType.Action); p.Deck.Hand.Add(second); CardPlayActions.Play(p, c, second, definition);
            Assert.That(move.TryMoveUsingPool(p, new AxialCoord(1, -1), map, c), Is.True); Assert.That(c.MovementPool, Is.EqualTo(1));
            Assert.That(c.CurrentTerrain, Is.EqualTo(TerrainType.Forest)); Assert.That(p.Mana.GetTotal(), Is.Zero);
        }

        [Test] public void InsufficientActualSupplyAndAlreadyPopulatedMapRejectWithoutConsumption()
        {
            var catalog = OriginalMapCatalog.Load(Source); var map = new MapState(); var supply = Supply(1); var token = supply.DrawPile.Single();
            Assert.Throws<InvalidOperationException>(() => catalog.Initialize(map, supply));
            Assert.That(map.OriginalCells, Is.Empty); Assert.That(map.Placed, Is.Empty); Assert.That(map.Places, Is.Empty);
            Assert.That(supply.DrawPile.Single(), Is.SameAs(token)); Assert.That(supply.InUse, Is.Empty);
            var enough = Supply(); catalog.Initialize(map, enough); var before = enough.InUse.ToArray();
            Assert.Throws<InvalidOperationException>(() => catalog.Initialize(map, enough));
            Assert.That(map.OriginalCells.Count, Is.EqualTo(21)); Assert.That(enough.InUse.Count, Is.EqualTo(2));
            Assert.That(enough.InUse[0], Is.SameAs(before[0])); Assert.That(enough.InUse[1], Is.SameAs(before[1]));
        }

        [TestCase("overlap")] [TestCase("sixCells")] [TestCase("rotation")]
        public void InvalidOriginalLayoutRejectsDuringPreflightWithoutTouchingSupply(string invalid)
        {
            var json = JObject.Parse(File.ReadAllText(Source)); var supply = Supply(); var before = supply.DrawPile.ToArray();
            if (invalid == "overlap") { json["initialPlacement"][1]["anchorQ"] = 0; json["initialPlacement"][1]["anchorR"] = 0; }
            if (invalid == "sixCells") ((JArray)json["tiles"][0]["cells"]).RemoveAt(0);
            if (invalid == "rotation") json["tiles"][0]["orientation"] = "rotated";
            Assert.Throws<InvalidDataException>(() => OriginalMapCatalog.Parse(json.ToString()).Initialize(new MapState(), supply));
            Assert.That(supply.InUse, Is.Empty); Assert.That(supply.DrawPile.Count, Is.EqualTo(before.Length));
            for (int i = 0; i < before.Length; i++) Assert.That(supply.DrawPile[i], Is.SameAs(before[i]));
        }
    }
}
