using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Tests.map
{
    public class TerrainCostTests
    {
        [Test]
        public void TestForestCosts()
        {
            Assert.AreEqual(3, TerrainCost.GetCost(TerrainType.Forest, DayPart.Day));
            Assert.AreEqual(2, TerrainCost.GetCost(TerrainType.Forest, DayPart.Night));
        }

        [Test]
        public void TestDesertCosts()
        {
            Assert.AreEqual(2, TerrainCost.GetCost(TerrainType.Desert, DayPart.Day));
            Assert.AreEqual(3, TerrainCost.GetCost(TerrainType.Desert, DayPart.Night));
        }

        [Test]
        public void TestMountainCosts()
        {
            Assert.AreEqual(3, TerrainCost.GetCost(TerrainType.Mountain, DayPart.Day));
            Assert.AreEqual(4, TerrainCost.GetCost(TerrainType.Mountain, DayPart.Night));
        }

        [Test]
        public void TestSwampCosts()
        {
            Assert.AreEqual(3, TerrainCost.GetCost(TerrainType.Swamp, DayPart.Day));
            Assert.AreEqual(5, TerrainCost.GetCost(TerrainType.Swamp, DayPart.Night));
        }

        [Test]
        public void TestPlainsCosts()
        {
            Assert.AreEqual(2, TerrainCost.GetCost(TerrainType.Plains, DayPart.Day));
            Assert.AreEqual(2, TerrainCost.GetCost(TerrainType.Plains, DayPart.Night));
        }

        [Test]
        public void TestSettlementsAreOneCost()
        {
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.Village, DayPart.Day));
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.Village, DayPart.Night));
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.City, DayPart.Day));
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.City, DayPart.Night));
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.Keep, DayPart.Day));
            Assert.AreEqual(1, TerrainCost.GetCost(TerrainType.Keep, DayPart.Night));
        }

        [Test]
        public void TestWaterIsImpassable()
        {
            Assert.AreEqual(int.MaxValue, TerrainCost.GetCost(TerrainType.Lake, DayPart.Day));
            Assert.AreEqual(int.MaxValue, TerrainCost.GetCost(TerrainType.River, DayPart.Day));
            Assert.False(TerrainCost.IsPassable(TerrainType.Lake, DayPart.Day));
            Assert.False(TerrainCost.IsPassable(TerrainType.River, DayPart.Day));
        }

        [Test]
        public void TestPassableCheck()
        {
            Assert.True(TerrainCost.IsPassable(TerrainType.Forest, DayPart.Day));
            Assert.True(TerrainCost.IsPassable(TerrainType.Desert, DayPart.Night));
            Assert.False(TerrainCost.IsPassable(TerrainType.Lake, DayPart.Day));
        }
    }
}