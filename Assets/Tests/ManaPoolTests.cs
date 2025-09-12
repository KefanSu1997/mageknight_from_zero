using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;

namespace MK.Tests.game
{
    public class ManaPoolTests
    {
        private ManaSource _manaSource;
        private PlayerState _player1;
        private PlayerState _player2;

        [SetUp]
        public void Setup()
        {
            Func<DayPart> timeProvider = () => DayPart.Day;
            _manaSource = new ManaSource(2, timeProvider, new System.Random(42));
            _player1 = new PlayerState(1, "Alice");
            _player2 = new PlayerState(2, "Bob");
        }

        [Test]
        public void TestInitialDiceCount()
        {
            Assert.AreEqual(4, _manaSource.Dice.Count); // 2 players + 2 = 4 dice
        }

        [Test]
        public void TestTakeAnyDice()
        {
            var die = _manaSource.TakeAny(_player1);
            Assert.IsNotNull(die);
            Assert.AreEqual(die, _player1.HeldManaDie);
            Assert.AreEqual(3, _manaSource.Dice.Count);
        }

        [Test]
        public void TestTakeSpecificColor()
        {
            var beforeCount = _manaSource.GetAvailableCount(MK.Logic.Core.ManaColor.Red);
            var die = _manaSource.Take(_player1, MK.Logic.Core.ManaColor.Red);
            
            if (die != null)
            {
                Assert.AreEqual(MK.Logic.Core.ManaColor.Red, die.Face);
                Assert.AreEqual(die, _player1.HeldManaDie);
            }
        }

        [Test]
        public void TestPlayerCannotTakeMultipleDice()
        {
            _manaSource.TakeAny(_player1);
            Assert.Throws<InvalidOperationException>(() => _manaSource.TakeAny(_player1));
        }

        [Test]
        public void TestReturnDice()
        {
            var die = _manaSource.TakeAny(_player1);
            var initialFace = die.Face;
            
            _manaSource.Return(die);
            
            Assert.IsNull(_player1.HeldManaDie);
            Assert.AreEqual(4, _manaSource.Dice.Count);
        }

        [Test]
        public void TestResetSourceLimitsBlackGoldRatio()
        {
            // Force a high number of black/gold dice
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                _manaSource.ResetSource();
                var blackGoldCount = _manaSource.Dice.Count(d => d.Face is MK.Logic.Core.ManaColor.Black or MK.Logic.Core.ManaColor.Gold);
                Assert.LessOrEqual(blackGoldCount * 2, _manaSource.Dice.Count);
            }
        }

        [Test]
        public void TestDiceDistributionIsBalanced()
        {
            var dist = _manaSource.GetDiceDistribution();
            Assert.IsTrue(dist.Values.Sum() >= 3); // At least 3 non-black/gold dice
        }

        [Test]
        public void TestPrintPoolStateIsFormatted()
        {
            var state = _manaSource.PrintPoolState();
            Assert.IsNotEmpty(state);
            Assert.IsTrue(state.Contains("魔力池:"));
            Assert.IsTrue(state.Contains("总计"));
        }

        [Test]
        public void TestReturnAllPlayersDice()
        {
            _manaSource.TakeAny(_player1);
            _manaSource.TakeAny(_player2);
            
            Assert.AreEqual(2, _manaSource.Dice.Count);
            
            var players = new[] { _player1, _player2 };
            _manaSource.ReturnAllPlayersDice(players);
            
            Assert.AreEqual(4, _manaSource.Dice.Count);
            Assert.IsNull(_player1.HeldManaDie);
            Assert.IsNull(_player2.HeldManaDie);
        }
    }
}