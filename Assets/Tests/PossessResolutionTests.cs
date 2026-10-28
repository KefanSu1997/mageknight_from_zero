using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MageKnight.Tests
{
    public sealed class PossessResolutionTests
    {
        [SetUp] public void SetUp() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PlayerState Player()
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black); return p;
        }

        private static Monster Enemy(int attack, Element element = Element.Physical, params Ability[] abilities)
            => new("target", 4, attack, element, 3, abilities);

        private static void Play(PlayerState player, ActionContext ctx, int target = 0)
        {
#if UNITY_EDITOR
            var card = AssetDatabase.LoadAssetAtPath<SpellCardSO>("Assets/GameData/CardsAssets/magic_021.asset");
            var data = new ActionCardData(card.Id, card.NameCn, card.Set, card.ImagePath, card.EnImagePath,
                card.Top.Effect, card.Bottom.Effect, card.Bottom.ManaCost);
#else
            var card = CardJsonLoader.LoadSpells().Single(c => c.Id == "magic_021");
            var data = new ActionCardData(card.Id, card.Name, card.Set, card.ImagePath, card.EnImagePath,
                card.Top.Effect, card.Bottom.Effect, card.Bottom.ManaCost);
#endif
            new ActionSystem().Play(data, player, ctx, true, target);
        }

        [TestCase(Element.Physical)] [TestCase(Element.Fire)] [TestCase(Element.Ice)] [TestCase(Element.ColdFire)]
        public void CopiesActualAttackAndElementWithoutAbilities(Element element)
        {
            var p = Player(); var enemies = new[] { Enemy(7, element, Ability.Brutal, Ability.Swift, Ability.Poison), Enemy(0) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies };
            Play(p, ctx);
            Assert.That(ctx.CombatPower.Attacks.Single(), Is.EqualTo(new AttackProfile(AttackType.Melee, 7, element, ExcludedTargetIndex: 0, ExcludedEnemy: enemies[0])));
            var result = CardCombatActions.Attack(p, ctx, enemies, new[] { 1 }, Phase.Melee);
            Assert.That(result.PrintedPower, Is.EqualTo(7)); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
        }

        [TestCase(-1)] [TestCase(1)]
        public void InvalidTargetRejectsBeforePayment(int target)
        {
            var p = Player(); var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = new[] { Enemy(4) } };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, target));
            Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ ManaColor.Black ], Is.EqualTo(1));
            Assert.That(ctx.SkipAttackIndices, Is.Empty); Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [Test]
        public void OwnTargetIsRejectedWithoutConsumingBorrowedPower()
        {
            var p = Player(); var enemies = new[] { Enemy(4), Enemy(0) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies }; Play(p, ctx);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, ctx, enemies, new[] { 0 }, Phase.Melee));
            Assert.That(ctx.MeleePool, Is.EqualTo(4));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, ctx, enemies, new[] { 0, 1 }, Phase.Melee));
            Assert.That(ctx.MeleePool, Is.EqualTo(4));
            Assert.That(CardCombatActions.Attack(p, ctx, enemies, new[] { 1 }, Phase.Melee).Kills, Is.EqualTo(1));
        }

        [Test]
        public void OrdinaryPowerCanAttackPossessedEnemyAndLeavesBorrowedContribution()
        {
            var p = Player(); var enemies = new[] { Enemy(4), Enemy(0) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies, MeleePool = 4 }; Play(p, ctx);
            Assert.That(CardCombatActions.Attack(p, ctx, enemies, new[] { 0 }, Phase.Melee).PrintedPower, Is.EqualTo(4));
            Assert.That(ctx.MeleePool, Is.EqualTo(4)); Assert.That(ctx.CombatPower.Attacks.Single().ExcludedTargetIndex, Is.Zero);
            Assert.That(CardCombatActions.Attack(p, ctx, enemies, new[] { 1 }, Phase.Melee).Kills, Is.EqualTo(1));
            Assert.That(ctx.MeleePool, Is.Zero);
        }

        [Test]
        public void PossessedAttackIsNotConvertedIntoPrematureRangedPower()
        {
            var p = Player(); var enemies = new[] { Enemy(4), Enemy(0) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies, NextMeleeAsRanged = true }; Play(p, ctx);
            Assert.That(ctx.RangedPool, Is.Zero); Assert.That(ctx.MeleePool, Is.EqualTo(4));
            Assert.That(ctx.NextMeleeAsRanged, Is.True);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, ctx, enemies, new[] { 1 }, Phase.Ranged));
        }

        [Test]
        public void LowerLevelAllocationCannotBypassSourceRestrictionOrMutateWounds()
        {
            var p = Player(); var enemies = new[] { Enemy(4), Enemy(9) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies }; Play(p, ctx);
            Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(p, enemies, Array.Empty<BlockAllocation>(),
                new[] { new AttackAllocation(new[] { 0 }, 4, Element.Physical, Components: ctx.CombatPower.Attacks) }, ctx: ctx));
            Assert.That(p.Wounds, Is.Zero); Assert.That(p.Fame, Is.Zero);
        }

        [Test]
        public void SelectedSecondEnemySkipsDamageAndBattleEndExpiresAllAttackPower()
        {
            var p = Player(); var enemies = new[] { Enemy(0), Enemy(8, Element.Fire, Ability.Poison) };
            var ctx = new ActionContext { InBattle = true, DayPart = DayPart.Night, Enemies = enemies, MeleePool = 2 }; Play(p, ctx, 1);
            BattleResolver.Resolve(p, enemies, Array.Empty<BlockAllocation>(), Array.Empty<AttackAllocation>(), ctx: ctx);
            Assert.That(p.Wounds, Is.Zero); ctx.SkipAttackIndices.Add(0);
            UnitDamageResolver.EndBattle(p, ctx);
            Assert.That(ctx.PossessedEnemyIndices, Is.Empty); Assert.That(ctx.SkipAttackIndices, Is.Empty);
            Assert.That(ctx.MeleePool, Is.Zero);
        }
    }
}
