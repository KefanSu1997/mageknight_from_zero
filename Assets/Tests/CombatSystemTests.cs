using NUnit.Framework;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Runtime;

namespace MK.Tests.game
{
    public class CombatSystemTests
    {
        [Test]
        public void TestDamagePacketConstruction()
        {
            var originalDamage = new DamagePacket(Element.Physical, 5);
            
            Assert.AreEqual(5, originalDamage.Amount);
            Assert.AreEqual(Element.Physical, originalDamage.Element);
        }

        [Test]
        public void TestElementalDamageTypes()
        {
            var fireDamage = new DamagePacket(Element.Fire, 3);
            var iceDamage = new DamagePacket(Element.Ice, 4);
            var coldFire = new DamagePacket(Element.ColdFire, 6);
            
            Assert.AreEqual(3, fireDamage.Amount);
            Assert.AreEqual(4, iceDamage.Amount);
            Assert.AreEqual(6, coldFire.Amount);
            
            Assert.AreEqual(Element.Fire, fireDamage.Element);
            Assert.AreEqual(Element.Ice, iceDamage.Element);
            Assert.AreEqual(Element.ColdFire, coldFire.Element);
        }

        [Test]
        public void TestAttackTypeEnumeration()
        {
            Assert.AreEqual(0, (int)AttackType.Ranged);
            Assert.AreEqual(1, (int)AttackType.Siege);
            Assert.AreEqual(2, (int)AttackType.Melee);
        }

        [Test]
        public void TestCombatPhaseEnumeration()
        {
            Assert.AreEqual(0, (int)Phase.None);
            Assert.AreEqual(1, (int)Phase.Ranged);
            Assert.AreEqual(2, (int)Phase.Block);
            Assert.AreEqual(3, (int)Phase.AssignDamage);
            Assert.AreEqual(4, (int)Phase.Melee);
            Assert.AreEqual(5, (int)Phase.End);
        }

        [Test]
        public void TestAbilityEffects()
        {
            Assert.AreEqual("Fortified", Ability.Fortified.ToString());
            Assert.AreEqual("Swift", Ability.Swift.ToString());
            Assert.AreEqual("Brutal", Ability.Brutal.ToString());
            Assert.AreEqual("Poison", Ability.Poison.ToString());
            Assert.AreEqual("Paralyze", Ability.Paralyze.ToString());
        }

        // [Test]
        // public void TestRangedResultCalculation()
        // {
        //     var ranged = new RangedResult(3, 2, true); // 3 damage, 2 blocked, fortified
        //     
        //     Assert.AreEqual(3, ranged.Blocked);
        //     Assert.AreEqual(2, ranged.Unblocked);
        //     Assert.IsTrue(ranged.TargetIsFortified);
        // }
    }
}