using System.IO;
using System.Linq;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

namespace MK.Tests.Map
{
    public sealed class OriginalMapCheckpointTests
    {
        [Test] public void ReadonlyPrintedCellCollectionKeepsOriginalPhysicalCellsOnUndo()
        {
            var supply=new EnemyTokenSupply(MonsterJsonLoader.ParseMonsters(File.ReadAllText("resources/text_json/monster.json")).Where(m=>m.TokenColor==EnemyTokenColor.Green));
            var map=new MapState();OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json").Initialize(map,supply);
            var tile=map.OriginalTiles.Values.First();var collection=tile.Cells;var cell=collection[0];var terrain=cell.TerrainName;
            var undo=DemoStateCheckpoint.Capture(map,supply);
            Assert.That(typeof(OriginalMapCell).GetProperty(nameof(OriginalMapCell.TerrainName)).GetSetMethod(),Is.Null,
                "Printed terrain cannot be assigned through the public API; undo restores mutable map containers.");
            map.OriginalCells.Clear();map.Placed.Clear();
            undo.Restore();
            Assert.That(tile.Cells,Is.SameAs(collection));Assert.That(tile.Cells[0],Is.SameAs(cell));
            Assert.That(cell.TerrainName,Is.EqualTo(terrain));Assert.That(map.OriginalCells.Count,Is.EqualTo(21));
            Assert.That(map.Placed.Count,Is.EqualTo(18));
        }
    }
}
