using NUnit.Framework;
using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;

namespace MK.Tests.map
{
    public class ExplorationTests
    {
        private ExplorationService _explorationService;
        private MapState _mapState;
        private TileDeck _countrysideDeck;
        private TileDeck _coreDeck;
        private PlayerState _player;

        [SetUp]
        public void Setup()
        {
            _countrysideDeck = new TileDeck();
            _coreDeck = new TileDeck();
            _mapState = new MapState
            {
                Countryside = _countrysideDeck,
                Core = _coreDeck
            };
            _explorationService = new ExplorationService(_mapState, new Random(42));
            _player = new PlayerState(1, "TestPlayer");
            _mapState.Placed[_player.Position] = new MapTile(TileSet.Countryside, 0, new TerrainType[6]);
        }

        [Test]
        public void TestExploreWithInsufficientMovement()
        {
            var result = _explorationService.Explore(_player, new AxialCoord(1, 0), 0, 1);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("探索需要2点移动力", result.ErrorMessage);
        }

        [Test]
        public void TestExploreSuccessWithSufficientMovement()
        {
            var tile = new MapTile();
            _countrysideDeck.SetCards(new[] { tile });
            
            var result = _explorationService.Explore(_player, new AxialCoord(1, 0), 0, 3);
            
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.RemainingMovement); // 3 - 2 = 1
            Assert.AreEqual(tile, result.Tile);
        }

        [Test]
        public void TestExploreWithRotation()
        {
            var tile = new MapTile();
            _countrysideDeck.SetCards(new[] { tile });
            
            var result = _explorationService.Explore(_player, new AxialCoord(1, 0), 180, 3);
            
            Assert.AreEqual(180, tile.Rotation);
        }

        [Test]
        public void TestTileDrawingOrder()
        {
            var countrysideTile = new MapTile();
            var coreTile = new MapTile();
            
            _countrysideDeck.SetCards(new[] { countrysideTile });
            _coreDeck.SetCards(new[] { coreTile });
            
            var result = _explorationService.Explore(_player, new AxialCoord(1, 0), 0, 3);
            
            // Should draw from countryside first
            Assert.AreEqual(countrysideTile, result.Tile);
            Assert.AreEqual(0, _countrysideDeck.Count);
            Assert.AreEqual(1, _coreDeck.Count);
        }
    }
}
