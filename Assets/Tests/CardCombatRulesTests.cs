using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CardCombatRulesTests
    {
        private static Monster Enemy(int armor = 3, params Ability[] abilities)
            => new("target", armor, 0, Element.Physical, 2, abilities);

        [TestCase(Element.Fire, Ability.IceResist, 1)]
        [TestCase(Element.Ice, Ability.FireResist, 1)]
        [TestCase(Element.Fire, Ability.FireResist, 0.5)]
        [TestCase(Element.Physical, Ability.PhysicalResist, 0.5)]
        public void ThereIsNoOppositeElementWeakness(Element element, Ability ability, double expected)
            => Assert.That(Constants.Efficiency(element, ability), Is.EqualTo(expected));

        [Test]
        public void MixedInefficientAttacksAreSummedBeforeRounding()
        {
            var enemies = new[] { Enemy(3, Ability.FireResist, Ability.IceResist) };
            var parts = new[] { new AttackProfile(AttackType.Ranged, 3, Element.Fire), new AttackProfile(AttackType.Ranged, 3, Element.Ice) };
            Assert.That(CombatMath.EffectiveAttack(enemies, new[] { 0 }, parts, Phase.Ranged), Is.EqualTo(3));
        }

        [Test]
        public void ColdFireRequiresBothResistancesOnTheSameEnemy()
        {
            var separate = new[] { Enemy(2, Ability.FireResist), Enemy(2, Ability.IceResist) };
            var part = new[] { new AttackProfile(AttackType.Melee, 7, Element.ColdFire) };
            Assert.That(CombatMath.EffectiveAttack(separate, new[] { 0, 1 }, part, Phase.Melee), Is.EqualTo(7));
            Assert.That(CombatMath.EffectiveAttack(new[] { Enemy(2, Ability.FireResist, Ability.IceResist) }, new[] { 0 }, part, Phase.Melee), Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveAttack(new[] { Enemy(2, Ability.MagicResist) }, new[] { 0 }, part, Phase.Melee), Is.EqualTo(7));
        }

        [Test]
        public void InefficientBlocksAreSummedBeforeRoundingInTheActualResolver()
        {
            var player = new PlayerState();
            var enemy = new Monster("fire", 5, 3, Element.Fire, 2, Array.Empty<Ability>());
            var blocks = new[] { new BlockAllocation(0, 3, Element.Physical), new BlockAllocation(0, 3, Element.Fire) };
            var result = BattleResolver.Resolve(player, new[] { enemy }, blocks, Array.Empty<AttackAllocation>());
            Assert.That(result.TotalWounds, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
        }

        [Test]
        public void SuccessiveMeleeAttacksKeepOriginalIndicesAndDoNotRepeatRewards()
        {
            var player = new PlayerState();
            var attacks = new[] { new AttackAllocation(new[] { 0, 0 }, 3, Element.Physical),
                new AttackAllocation(new[] { 0 }, 3, Element.Physical), new AttackAllocation(new[] { 1 }, 3, Element.Physical) };
            var result = BattleResolver.Resolve(player, new[] { Enemy(), Enemy() }, Array.Empty<BlockAllocation>(), attacks);
            Assert.That(result.AllKilled, Is.True);
            Assert.That(result.KilledIndices, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(player.Fame, Is.EqualTo(4));
        }

        [Test]
        public void ForbiddenRangedTargetPreservesPowerButLowAttackSpendsIt()
        {
            var player = new PlayerState();
            var context = new ActionContext();
            new FireballEffect(false).Execute(player, context);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(player, context,
                new[] { Enemy(3, Ability.Fortified) }, new[] { 0 }, Phase.Ranged));
            Assert.That(context.RangedPool, Is.EqualTo(5));
            var result = CardCombatActions.Attack(player, context, new[] { Enemy(3, Ability.FireResist) }, new[] { 0 }, Phase.Ranged);
            Assert.That(result.EffectivePower, Is.EqualTo(2));
            Assert.That(result.Kills, Is.Zero);
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
        }

        [Test]
        public void DoubleFortificationBlocksSiegeOnlyBeforeMelee()
        {
            var player = new PlayerState();
            var context = new ActionContext { FortifiedSite = true };
            new FireballEffect(true).Execute(player, context);
            Assert.That(context.SiegePool, Is.EqualTo(8));
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(player, context,
                new[] { Enemy(8, Ability.Fortified) }, new[] { 0 }, Phase.Ranged));
            var result = CardCombatActions.Attack(player, context, new[] { Enemy(8, Ability.Fortified) }, new[] { 0 }, Phase.Melee);
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(context.SiegePool, Is.Zero);
        }

        [Test]
        public void ConversionPreservesElementAndEarlierContributions()
        {
            var pool = new CombatPowerPool();
            pool.AddAttack(2);
            int start = pool.Attacks.Count;
            pool.AddAttack(3, Element.Fire);
            pool.ConvertNewMeleeToRanged(start);
            Assert.That(pool.Total(AttackType.Melee), Is.EqualTo(2));
            Assert.That(pool.Attacks[1].Element, Is.EqualTo(Element.Fire));
            Assert.That(pool.Total(AttackType.Ranged), Is.EqualTo(3));
            pool.ConsumeAttacks(Phase.Ranged);
            Assert.That(pool.Total(AttackType.Melee), Is.EqualTo(2));
        }
    }
}
