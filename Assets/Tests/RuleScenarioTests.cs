using System;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Scenarios;

namespace MK.Tests.Rules
{
    public class RuleScenarioTests
    {
        [TestCase(3, 2)]
        [TestCase(4, 0)]
        public void PartialBlockNeverReducesDamage(int block, int wounds)
        {
            var session = new CombatLessonSession();
            for (int i = 0; i < block; i++) session.Apply("block+");
            session.Apply("resolve");
            Assert.AreEqual(wounds, session.Player.Wounds);
            Assert.AreEqual(3, session.Player.Fame);
            Assert.IsFalse(session.Apply("resolve"));
            Assert.AreEqual(3, session.Player.Fame);
        }

        [TestCase("swift", 4, 2)]
        [TestCase("swift", 8, 0)]
        [TestCase("brutal", 3, 4)]
        [TestCase("brutal", 4, 0)]
        public void EnemyAbilitiesUseSeparateBlockAndDamageRules(string encounter, int block, int wounds)
        {
            var session = new CombatLessonSession(); session.Apply("case:" + encounter);
            for (int i = 0; i < block; i++) session.Apply("block+");
            session.Apply("resolve"); Assert.AreEqual(wounds, session.Player.Wounds);
        }

        [Test]
        public void FireAttackNeedsIceBlockAndFireResistanceHalvesAttack()
        {
            var session = new CombatLessonSession(); session.Apply("case:fire");
            for (int i = 0; i < 4; i++) session.Apply("block+");
            session.Apply("attack:fire"); session.Apply("resolve");
            Assert.AreEqual(2, session.Player.Wounds); Assert.AreEqual(0, session.Player.Fame); Assert.IsTrue(session.EnemyAlive);
            session.Apply("reset");
            for (int i = 0; i < 4; i++) session.Apply("block+");
            session.Apply("block:ice"); session.Apply("resolve");
            Assert.AreEqual(0, session.Player.Wounds); Assert.AreEqual(3, session.Player.Fame);
        }

        [Test]
        public void ExploreThenEnterPaysSeparateCostsAndRejectsInsufficientMove()
        {
            var session = new ExplorationLessonSession();
            Assert.IsTrue(session.Apply("move")); Assert.AreEqual(3, session.Movement);
            session.Apply("select:frontier"); Assert.IsTrue(session.Apply("explore"));
            Assert.AreEqual(1, session.Movement); Assert.AreEqual(new AxialCoord(1, 0), session.Player.Position);
            Assert.IsFalse(session.Apply("move")); Assert.AreEqual(1, session.Movement);
            Assert.AreEqual(new AxialCoord(1, 0), session.Player.Position);
            Assert.IsFalse(session.Apply("explore")); Assert.AreEqual(7, session.Map.Placed.Count);
        }

        [Test]
        public void NightForestCostsFiveAndLakeIsInaccessible()
        {
            var session = new ExplorationLessonSession(); session.Apply("night"); session.Apply("move");
            Assert.AreEqual(1, session.Movement);
            session.Apply("reset"); session.Apply("select:lake");
            Assert.IsFalse(session.Apply("move")); Assert.AreEqual(6, session.Movement);
            Assert.AreEqual(new AxialCoord(0, 0), session.Player.Position);
            session.Apply("select:frontier"); Assert.IsFalse(session.Apply("explore"));
            Assert.AreEqual(1, session.Map.Countryside.Count);
        }

        [Test]
        public void RecruitingChecksLocationResourcesSlotsAndDuplicateOffer()
        {
            var session = new RecruitmentLessonSession();
            Assert.IsFalse(session.Apply("recruit")); Assert.AreEqual(4, session.Influence);
            session.Apply("influence"); Assert.AreEqual(6, session.Influence);
            Assert.IsFalse(session.Apply("influence")); Assert.AreEqual(6, session.Influence);
            Assert.IsTrue(session.Apply("recruit")); Assert.AreEqual(1, session.Influence);
            Assert.IsFalse(session.Apply("recruit")); Assert.AreEqual(1, session.Player.Units.Count);
            session.Apply("reset"); session.Apply("select:2");
            Assert.IsFalse(session.Apply("recruit")); Assert.AreEqual(4, session.Influence);
        }

        [Test]
        public void ReputationAppliesOnceAcrossTwoPurchases()
        {
            var session = new RecruitmentLessonSession(); session.Apply("case:reputation");
            Assert.AreEqual(12, session.Influence); Assert.AreEqual(2, session.Player.CommandSlots);
            session.Apply("recruit"); Assert.AreEqual(7, session.Influence);
            session.Apply("select:1"); session.Apply("recruit");
            Assert.AreEqual(0, session.Influence); Assert.AreEqual(2, session.Player.Units.Count);
        }

        [Test]
        public void InitialCommandLimitComesFromLevelNotReputation()
        {
            var player = new PlayerState { Reputation = 7 };
            Assert.AreEqual(1, player.CommandSlots);
            player.Fame = 8; Assert.AreEqual(3, player.Level); Assert.AreEqual(2, player.CommandSlots);
        }

        [Test]
        public void FullCommandSlotsRejectRecruitEvenWithExactInfluence()
        {
            var session = new RecruitmentLessonSession(); session.Apply("case:capacity");
            session.Apply("recruit"); session.Apply("select:1");
            Assert.AreEqual(7, session.Influence); Assert.AreEqual(7, session.Cost);
            Assert.IsFalse(session.Apply("recruit"));
            Assert.AreEqual(7, session.Influence); Assert.AreEqual(1, session.Player.Units.Count);
        }

        [Test]
        public void ReturnedManaDieClearsHolderAndCannotDuplicateTheSource()
        {
            var source = new ManaSource(1, () => DayPart.Day, new Random(42));
            var player = new PlayerState(); var die = source.TakeAny(player);
            source.Return(die); source.Return(die);
            Assert.IsNull(player.HeldManaDie); Assert.AreEqual(3, source.Dice.Count);
        }

        [Test]
        public void FailedMultiColorPaymentIsAtomic()
        {
            var player = new PlayerState { ManaCurseColor = ManaColor.Red };
            player.Mana.AddToken(ManaColor.Red);
            bool paid = player.Mana.Pay(new ManaCost(new() { { Element.Fire, 1 }, { Element.Ice, 1 } }), player);
            Assert.IsFalse(paid); Assert.AreEqual(1, player.Mana.Tokens[ManaColor.Red]);
            Assert.AreEqual(0, player.Wounds); Assert.IsFalse(player.ManaCurseTriggered);
        }

        [Test]
        public void TwoTurnJourneyConservesCardsAndRewardsOnlyOnce()
        {
            var session = new JourneyLessonSession();
            Assert.IsFalse(session.Apply("recruit"));
            foreach (string action in new[] { "march", "explore", "travel", "talk", "recruit", "endTurn" }) Assert.IsTrue(session.Apply(action), action);
            Assert.AreEqual(5, session.Hand); Assert.AreEqual(9, session.Deck); Assert.AreEqual(2, session.Discard);
            Assert.AreEqual(0, session.Movement); Assert.AreEqual(0, session.Influence);
            Assert.AreEqual(1, session.Player.Mana.Crystals[ManaColor.Red]);
            Assert.IsTrue(session.Apply("unit")); Assert.IsFalse(session.Apply("unit"));
            Assert.IsTrue(session.Apply("boost")); Assert.IsFalse(session.Apply("boost"));
            Assert.IsTrue(session.Apply("battle")); Assert.IsTrue(session.Completed);
            Assert.AreEqual(3, session.Player.Fame); Assert.AreEqual(0, session.Player.Wounds);
            Assert.AreEqual(16, session.Hand + session.Deck + session.Discard + session.Played);
            Assert.IsFalse(session.Apply("battle")); Assert.AreEqual(3, session.Player.Fame);
        }

        [Test]
        public void BasicAttackCannotDefeatArmorFive()
        {
            var session = new JourneyLessonSession();
            foreach (string action in new[] { "march", "explore", "travel", "talk", "recruit", "endTurn", "unit", "strike", "battle" }) session.Apply(action);
            Assert.IsFalse(session.Completed); Assert.AreEqual(0, session.Player.Fame);
            Assert.AreEqual(1, session.Player.Mana.Crystals[ManaColor.Red]);
        }

        [Test]
        public void ExplorationRotationOccursOnceAndEmptyDeckReturnsFailure()
        {
            var map = new MapState(); var player = new PlayerState();
            map.Placed[player.Position] = ExplorationLessonSession.Tile(0, TerrainType.Plains);
            var tile = new MapTile(TileSet.Countryside, 1, new[] { TerrainType.Plains, TerrainType.Forest, TerrainType.Desert, TerrainType.Hills, TerrainType.Swamp, TerrainType.Wasteland });
            map.Countryside.Push(tile);
            var service = new ExplorationService(map, new FixedRandom());
            var result = service.Explore(player, new AxialCoord(1, 0), 60, 4);
            Assert.IsTrue(result.Success); Assert.AreEqual(TerrainType.Forest, tile.Edges[0]); Assert.AreEqual(60, tile.Rotation);
            Assert.IsFalse(service.Explore(player, new AxialCoord(0, 1), 0, 4).Success);
        }

        [Test]
        public void SpawnedThreatReturnsACombatEventInsteadOfThrowing()
        {
            var map = new MapState(); var player = new PlayerState();
            map.Placed[player.Position] = ExplorationLessonSession.Tile(0, TerrainType.Plains);
            map.Countryside.Push(ExplorationLessonSession.Tile(1, TerrainType.Plains));
            var result = new ExplorationService(map, new ThreatRandom()).Explore(player, new AxialCoord(1, 0), 0, 2);
            Assert.IsTrue(result.Success); Assert.IsNotNull(result.CombatEvent); Assert.AreEqual(0, result.RemainingMovement);
        }

        private sealed class FixedRandom : Random { public override int Next(int maxValue) => maxValue - 1; }
        private sealed class ThreatRandom : Random { public override int Next(int maxValue) => 0; }
    }
}
