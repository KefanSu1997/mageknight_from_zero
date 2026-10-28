using System;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MageKnight.Tests
{
    public sealed class OriginalUnitDamageTests
    {
        [SetUp] public void SetUp() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static UnitState Own(PlayerState player, string id)
        {
            var unit = new UnitState(CardJsonLoader.LoadUnit(id)); player.Units.Add(unit); return unit;
        }

        private static void Hit(PlayerState player, ActionContext context, int damage, UnitState[] units,
            Element element = Element.Physical, params Ability[] abilities)
            => BattleResolver.Resolve(player, new[] { new Monster("damage_rule_fixture", 3, damage, element, 2, abilities) },
                Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: context,
                damageAssignments: new[] { new UnitDamageAllocation(0, units) });

        [TestCase("army_2_006", 1, 1, 0)] [TestCase("army_2_006", 3, 1, 0)]
        [TestCase("army_2_006", 4, 1, 1)] [TestCase("army_2_006", 7, 1, 2)]
        [TestCase("army_2_001", 1, 0, 0)] [TestCase("army_2_001", 3, 0, 0)]
        [TestCase("army_2_001", 4, 1, 0)] [TestCase("army_2_001", 6, 1, 0)] [TestCase("army_2_001", 7, 1, 1)]
        public void OneUnitCannotSwallowAnEntireAttack(string id, int attack, int wounds, int heroWounds)
        {
            var player = new PlayerState(); var unit = Own(player, id); var context = new ActionContext();
            Hit(player, context, attack, new[] { unit });
            Assert.That(unit.Wounds, Is.EqualTo(wounds)); Assert.That(player.Wounds, Is.EqualTo(heroWounds));
            Assert.That(unit.IsReady, Is.True); Assert.That(context.DamageAssignedUnits.Count, Is.EqualTo(1));
        }

        [TestCase(Element.Fire)] [TestCase(Element.Ice)] [TestCase(Element.ColdFire)]
        public void PhysicalResistanceDoesNotAbsorbElementalDamage(Element element)
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_001");
            Hit(player, new ActionContext(), 4, new[] { unit }, element);
            Assert.That(unit.Wounds, Is.EqualTo(1)); Assert.That(player.Wounds, Is.EqualTo(1));
        }

        [Test]
        public void ResistanceWithoutWoundStillConsumesOneAssignmentUntilBattleEnds()
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_001"); var context = new ActionContext();
            unit.Exhaust(); Hit(player, context, 3, new[] { unit });
            Assert.Throws<InvalidOperationException>(() => Hit(player, context, 3, new[] { unit }));
            Assert.That(unit.Wounds, Is.Zero); Assert.That(player.Wounds, Is.Zero);
            UnitDamageResolver.EndBattle(player, context); Assert.That(unit.IsReady, Is.False);
            Hit(player, context, 3, new[] { unit }); Assert.That(unit.Wounds, Is.Zero);
        }

        [Test]
        public void MultipleUnitsAbsorbInOrderAndLeaveRealHeroWounds()
        {
            var player = new PlayerState { ExtraCommandSlots = 1 }; var first = Own(player, "army_2_001"); var second = Own(player, "army_2_006");
            Hit(player, new ActionContext(), 10, new[] { first, second });
            Assert.That(first.Wounds, Is.EqualTo(1)); Assert.That(second.Wounds, Is.EqualTo(1)); Assert.That(player.Wounds, Is.EqualTo(1));
        }

        [Test]
        public void InvalidSecondTargetRejectsBeforeAnyFirstUnitOrHeroMutation()
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_006");
            var foreign = new UnitState(CardJsonLoader.LoadUnit("army_2_006")); var context = new ActionContext();
            Assert.Throws<InvalidOperationException>(() => Hit(player, context, 7, new[] { unit, foreign }));
            Assert.That(unit.Wounds, Is.Zero); Assert.That(player.Wounds, Is.Zero); Assert.That(context.DamageAssignedUnits.Count, Is.Zero);
        }

        [Test]
        public void PoisonReplacesOneUnitWoundAndAddsHeroDiscardWoundsForOverflow()
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_006");
            Hit(player, new ActionContext(), 7, new[] { unit }, Element.Physical, Ability.Poison);
            Assert.That(unit.Wounds, Is.EqualTo(2)); Assert.That(player.Wounds, Is.EqualTo(2)); Assert.That(player.DiscardWounds, Is.EqualTo(2));
            unit.Heal(1); Assert.That(unit.CanActivate, Is.False); unit.Heal(1); Assert.That(unit.CanActivate, Is.True);
        }

        [TestCase(3, false, 1)] [TestCase(4, true, 0)]
        public void ParalyzeOnlyDestroysWhenResistanceFailsToPreventWound(int damage, bool destroyed, int count)
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_001");
            Hit(player, new ActionContext(), damage, new[] { unit }, Element.Physical, Ability.Paralyze);
            Assert.That(unit.IsDestroyed, Is.EqualTo(destroyed)); Assert.That(unit.Wounds, Is.Zero); Assert.That(player.Units.Count, Is.EqualTo(count));
        }

        [Test]
        public void LegacyAssignmentAlsoCarriesExcessToHero()
        {
            var player = new PlayerState(); var unit = Own(player, "army_2_006");
            BattleResolver.Resolve(player, new[] { new Monster("legacy_fixture", 3, 7, Element.Physical, 2, Array.Empty<Ability>()) },
                new[] { new BlockAllocation(0, 0, Element.Physical, ForUnitIndex: 0) }, Array.Empty<AttackAllocation>(), player.Units);
            Assert.That(unit.Wounds, Is.EqualTo(1)); Assert.That(player.Wounds, Is.EqualTo(2));
        }
    }
}
