using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
using UnityEngine;

namespace MK.Tests.Cards
{
    public sealed class CardElementalTriggerTests
    {
        [SetUp]
        public void SetSource() => CardJsonLoader.SetBaseDirectory(System.IO.Path.Combine(Application.dataPath, "../resources/text_json"));

        private static ActionCardData Spell(string id) => new(id, id, CardSet.Spell, "", "", "", "", Array.Empty<ManaColor>());
        private static Monster Enemy(int attack, Element element, params Ability[] abilities)
            => new("target", 5, attack, element, 2, abilities);

        [TestCase(false, ManaColor.Red, Element.ColdFire, AttackType.Melee, 7)]
        [TestCase(true, ManaColor.Red, Element.ColdFire, AttackType.Melee, 10)]
        [TestCase(false, ManaColor.Blue, Element.Ice, AttackType.Melee, 8)]
        [TestCase(true, ManaColor.Blue, Element.Ice, AttackType.Melee, 11)]
        [TestCase(false, ManaColor.White, Element.Ice, AttackType.Ranged, 6)]
        [TestCase(true, ManaColor.White, Element.Ice, AttackType.Ranged, 9)]
        [TestCase(false, ManaColor.Green, Element.Ice, AttackType.Siege, 5)]
        [TestCase(true, ManaColor.Green, Element.Ice, AttackType.Siege, 8)]
        public void ManaBoltPaysPrintedAndChosenCostsBeforeProducingTypedAttack(bool strong, ManaColor color, Element element, AttackType type, int value)
        {
            var player = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night };
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(color);
            if (strong) player.Mana.AddToken(ManaColor.Black);
            new ActionSystem().Play(Spell("magic_017"), player, ctx, strong, (int)color);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(ctx.CombatPower.Attacks.Single(), Is.EqualTo(new AttackProfile(type, value, element)));
        }

        [TestCase(false, ManaColor.Blue)] [TestCase(true, ManaColor.Blue)]
        [TestCase(false, ManaColor.Red)] [TestCase(true, ManaColor.Red)]
        public void MissingExtraManaPreservesWholePayment(bool strong, ManaColor chosen)
        {
            var player = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night };
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(() => new ActionSystem().Play(Spell("magic_017"), player, ctx, strong, (int)chosen));
            Assert.That(player.Mana.Tokens[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(player.Mana.Tokens[ManaColor.Black], Is.EqualTo(1));
            Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [TestCase("magic_017", 5)] [TestCase("magic_010", 2)]
        public void InvalidChoicePreservesPrintedMana(string id, int option)
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => new ActionSystem().Play(Spell(id), player, ctx, false, option));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(2));
        }

        [Test]
        public void IceShieldReducesOnlyActuallyBlockedTargetAndAttackUsesReduction()
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            var enemies = new[] { Enemy(3, Element.Fire), Enemy(3, Element.Fire) };
            new IceShieldEffect(true).Execute(player, ctx);
            Assert.That(ctx.ArmorReduction, Is.Empty);
            CardCombatActions.Block(player, ctx, enemies, 1);
            Assert.That(ctx.ArmorReduction.Keys, Is.EquivalentTo(new[] { 1 }));
            Assert.That(ctx.ArmorReduction[1], Is.EqualTo(3));
            ctx.CombatPower.AddAttack(2);
            var result = CardCombatActions.Attack(player, ctx, enemies, new[] { 1 }, Phase.Melee);
            Assert.That(result.RequiredPower, Is.EqualTo(2)); Assert.That(result.Kills, Is.EqualTo(1));
        }

        [TestCase(Ability.IceResist)] [TestCase(Ability.MagicResist)]
        public void ImmunityPreventsIceReductionWithoutRemovingIceBlock(Ability ability)
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            new IceShieldEffect(true).Execute(player, ctx);
            var result = CardCombatActions.Block(player, ctx, Enemy(3, Element.Fire, ability));
            Assert.That(result.Wounds, Is.Zero); Assert.That(ctx.ArmorReduction, Is.Empty);
        }

        [Test]
        public void FailedBlockDoesNotGrantBurningShieldAttack()
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            new BurningShieldEffect(false).Execute(player, ctx);
            var result = CardCombatActions.Block(player, ctx, Enemy(5, Element.Ice));
            Assert.That(result.Wounds, Is.GreaterThan(0)); Assert.That(ctx.MeleePool, Is.Zero);
            Assert.That(ctx.AttackAfterBlock, Is.Zero);
        }

        [Test]
        public void BurningShieldAttackStaysFireAndCanAttackAnotherTarget()
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            new BurningShieldEffect(false).Execute(player, ctx);
            var enemies = new[] { Enemy(4, Element.Ice), Enemy(3, Element.Physical, Ability.PhysicalResist) with { Armor = 4 } };
            CardCombatActions.Block(player, ctx, enemies, 0);
            var result = CardCombatActions.Attack(player, ctx, enemies, new[] { 1 }, Phase.Melee);
            Assert.That(result.EffectivePower, Is.EqualTo(4)); Assert.That(result.Kills, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateDestroyContributionsRewardOneEnemyOnlyOnce()
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            new BurningShieldEffect(true).Execute(player, ctx);
            new BurningShieldEffect(true).Execute(player, ctx);
            var result = CardCombatActions.Block(player, ctx, Enemy(8, Element.Ice));
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(player.Fame, Is.EqualTo(2));
        }

        [Test]
        public void ExplodingShieldCannotDestroySecondEnemyWithUnrelatedBlock()
        {
            var player = new PlayerState(); var ctx = new ActionContext();
            var enemies = new[] { Enemy(4, Element.Ice), Enemy(4, Element.Ice) };
            new BurningShieldEffect(true).Execute(player, ctx);
            Assert.That(CardCombatActions.Block(player, ctx, enemies, 0).Kills, Is.EqualTo(1));
            ctx.CombatPower.AddBlock(4, Element.Fire);
            Assert.That(CardCombatActions.Block(player, ctx, enemies, 1).Kills, Is.Zero);
            Assert.That(player.Fame, Is.EqualTo(2));
        }
    }
}
