using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class CardSiegeAndSplitBlockTests
    {
        private sealed class FixedRandom : Random
        {
            private readonly int _face;
            public int Calls { get; private set; }
            public FixedRandom(ManaColor face) => _face = (int)face;
            public override int Next(int maxValue) { Assert.That(maxValue, Is.EqualTo(6)); Calls++; return _face; }
        }
        private static Monster Enemy(int attack = 4, Element element = Element.Physical)
            => new("target", 5, attack, element, 2, Array.Empty<Ability>());

        [TestCase(ManaColor.Red, 1)] [TestCase(ManaColor.Black, 1)]
        [TestCase(ManaColor.Blue, 0)] [TestCase(ManaColor.Green, 0)]
        [TestCase(ManaColor.White, 0)] [TestCase(ManaColor.Gold, 0)]
        public void HornUsesAllSixFacesAtDayAndNight(ManaColor face, int wounds)
        {
            foreach (var day in new[] { DayPart.Day, DayPart.Night })
            {
                var random = new FixedRandom(face); var player = new PlayerState();
                var ctx = new ActionContext { DayPart = day, EffectRandom = random };
                new WrathHornEffect(false).Execute(player, ctx);
                Assert.That(player.Wounds, Is.EqualTo(wounds)); Assert.That(random.Calls, Is.EqualTo(1));
                Assert.That(ctx.SiegePool, Is.EqualTo(5)); Assert.That(ctx.RangedPool, Is.Zero);
                Assert.That(ctx.LastManaRolls.Single(), Is.EqualTo(face));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(5)]
        public void StrongHornRollsOncePerChosenBonus(int bonus)
        {
            var random = new FixedRandom(ManaColor.Black); var player = new PlayerState();
            var ctx = new ActionContext { EffectRandom = random };
            new WrathHornEffect(true).Execute(player, ctx, bonus);
            Assert.That(random.Calls, Is.EqualTo(bonus)); Assert.That(player.Wounds, Is.EqualTo(bonus));
            Assert.That(ctx.SiegePool, Is.EqualTo(5 + bonus));
        }

        [TestCase(-1)] [TestCase(6)]
        public void InvalidHornBonusDoesNotRollOrGrantPower(int bonus)
        {
            var random = new FixedRandom(ManaColor.Red); var ctx = new ActionContext { EffectRandom = random };
            Assert.Throws<InvalidOperationException>(() => new WrathHornEffect(true).Execute(new PlayerState(), ctx, bonus));
            Assert.That(random.Calls, Is.Zero); Assert.That(ctx.SiegePool, Is.Zero);
        }

        [TestCase(false, 2, Element.Physical)] [TestCase(true, 3, Element.ColdFire)]
        public void SplitShieldUsesOnlyFourForEachDistinctAttack(bool strong, int count, Element element)
        {
            var ctx = new ActionContext(); var player = new PlayerState();
            new NecroShieldEffect(strong).Execute(player, ctx, 1);
            var enemies = Enumerable.Range(0, count).Select(_ => Enemy(4, strong ? Element.ColdFire : Element.Physical)).ToArray();
            for (int i = 0; i < count; i++)
            {
                var result = CardCombatActions.Block(player, ctx, enemies, i);
                Assert.That(result.PrintedPower, Is.EqualTo(4)); Assert.That(result.Wounds, Is.Zero);
                Assert.That(ctx.BlockPool, Is.EqualTo((count - i - 1) * 4));
            }
        }

        [Test]
        public void SplitShieldCannotCombineItsPartsOrRepeatSameAttack()
        {
            var ctx = new ActionContext(); var player = new PlayerState();
            new NecroShieldEffect(true).Execute(player, ctx, 1);
            var enemies = new[] { Enemy(5), Enemy(4) };
            Assert.That(CardCombatActions.Block(player, ctx, enemies, 0).Wounds, Is.GreaterThan(0));
            Assert.That(ctx.BlockPool, Is.EqualTo(8));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Block(player, ctx, enemies, 0));
            Assert.That(ctx.BlockPool, Is.EqualTo(8));
            Assert.That(CardCombatActions.Block(player, ctx, enemies, 1).Wounds, Is.Zero);
        }

        [Test]
        public void NatureForceSiegeCanAttackFortifiedEnemyBeforeMelee()
        {
            var ctx = new ActionContext(); var player = new PlayerState();
            new NatureForceEffect(true).Execute(player, ctx);
            var enemy = Enemy() with { Armor = 3, Abilities = new[] { Ability.Fortified } };
            Assert.That(CardCombatActions.Attack(player, ctx, new[] { enemy }, new[] { 0 }, Phase.Ranged).Kills, Is.EqualTo(1));
        }
    }
}
