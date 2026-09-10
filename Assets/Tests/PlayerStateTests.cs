using NUnit.Framework;
using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Data;

namespace MK.Tests.game
{
    public class PlayerStateTests
    {
        [Test]
        public void TestPlayerInitialization()
        {
            var player = new PlayerState(1, "TestPlayer");
            
            Assert.AreEqual(1, player.Id);
            Assert.AreEqual("TestPlayer", player.Name);
            Assert.AreEqual(0, player.Reputation);
            Assert.AreEqual(0, player.Influence);
            Assert.AreEqual(1, player.Level);
            Assert.AreEqual(0, player.Fame);
        }

        [Test]
        public void TestManaDieAssignment()
        {
            var player = new PlayerState(1, "TestPlayer");
            var manaDie = new ManaDie(new System.Random());
            
            player.HeldManaDie = manaDie;
            Assert.AreEqual(manaDie, player.HeldManaDie);
        }

        [Test]
        public void TestReputationDoesNotCreateCommandTokens()
        {
            var player = new PlayerState(1, "TestPlayer");
            
            player.Reputation = 5;
            Assert.AreEqual(1, player.CommandSlots, "Reputation modifies interaction influence, not the unit command limit.");
        }
    }

    public class UnitStateTests
    {
        [Test]
        public void TestUnitInitialization()
        {
            var card = new UnitCard("test_unit", "测试单位", "Test Unit", 4, 0, 2, RecruitLocation.Village, 
                                   new AttackProfile[] { new(AttackType.Melee, 2, Element.Physical) }, 
                                   new Ability[] { });
            var unit = new UnitState(card);
            
            Assert.AreEqual(0, unit.Hull); // Compatibility property
            Assert.AreEqual(4, unit.Force); // Level as compatible property
            Assert.AreEqual(UnitStatus.Ready, unit.Status);
            Assert.IsFalse(unit.IsExhausted);
        }

        [Test]
        public void TestUnitExhaustion()
        {
            var card = new UnitCard("test_unit", "测试单位", "Test Unit", 4, 0, 2, RecruitLocation.Village, 
                                   new AttackProfile[] { new(AttackType.Melee, 2, Element.Physical) }, 
                                   new Ability[] { });
            var unit = new UnitState(card);
            
            unit.Exhaust();
            Assert.AreEqual(UnitStatus.Exhausted, unit.Status);
            Assert.IsTrue(unit.IsExhausted);
            
            unit.Restore();
            Assert.AreEqual(UnitStatus.Ready, unit.Status);
        }

        [Test]
        public void TestUnitFatigue()
        {
            var card = new UnitCard("test_unit", "测试单位", "Test Unit", 4, 0, 2, RecruitLocation.Village, 
                                   new AttackProfile[] { new(AttackType.Melee, 2, Element.Physical) }, 
                                   new Ability[] { });
            var unit = new UnitState(card);
            
            unit.AddWounds(4); // Make it fatigued
            Assert.IsTrue(unit.Fatigued);
            
            unit.Heal(2); // Heal some wounds
            Assert.IsFalse(unit.Fatigued);
        }
    }
}
