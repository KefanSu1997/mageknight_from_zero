using System;
using System.Collections.Generic;
using System.IO;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class OriginalUnitActivationTests
    {
        [SetUp] public void Setup() => CardJsonLoader.SetBaseDirectory(Path.GetFullPath("resources/text_json"));

        [TestCase("army_003", 2, 8, RecruitLocation.Keep | RecruitLocation.City)]
        [TestCase("army_2_001", 3, 7, RecruitLocation.Keep | RecruitLocation.MageTower)]
        [TestCase("army_2_005", 2, 7, RecruitLocation.Monastery)]
        [TestCase("army_2_006", 3, 4, RecruitLocation.Village)]
        [TestCase("army_2_007", 2, 7, RecruitLocation.Monastery)]
        [TestCase("army_2_008", 2, 7, RecruitLocation.Monastery)]
        [TestCase("army_2_012", 2, 6, RecruitLocation.Keep | RecruitLocation.Village)]
        [TestCase("army_2_014", 2, 6, RecruitLocation.Keep)]
        public void DefinitionsRetainOriginalAbilityCountsAndRecruitIcons(string id, int abilities, int cost, RecruitLocation places)
        {
            var card = CardJsonLoader.LoadUnit(id);
            Assert.That(card.Actions.Length, Is.EqualTo(abilities)); Assert.That(card.InfluenceCost, Is.EqualTo(cost));
            Assert.That(card.RecruitLocationMask, Is.EqualTo(places));
        }

        [TestCase("foreign")] [TestCase("wounded")] [TestCase("exhausted")] [TestCase("destroyed")]
        public void IllegalUnitStateCannotSpendManaOrCreatePower(string state)
        {
            var player = new PlayerState(); var ctx = new ActionContext { InBattle = true };
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_003"));
            if (state != "foreign") player.Units.Add(unit);
            if (state == "wounded") unit.AddWounds(1);
            if (state == "exhausted") unit.Exhaust();
            if (state == "destroyed") unit.Destroy();
            player.Mana.AddToken(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(player, ctx, unit, 1));
            Assert.That(player.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(ctx.RangedPool, Is.Zero);
        }

        [TestCase(-1)] [TestCase(2)]
        public void BadOptionDoesNotExhaustOrSpend(int option)
        {
            var player = new PlayerState(); var ctx = new ActionContext { InBattle = true };
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_007")); player.Units.Add(unit); player.Mana.AddToken(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(player, ctx, unit, 1, option));
            Assert.That(unit.IsReady, Is.True); Assert.That(player.Mana.Tokens[ManaColor.Red], Is.EqualTo(1));
            CardUnitActions.Use(player, ctx, unit, 1, 1);
            Assert.That(ctx.BlockPool, Is.EqualTo(4)); Assert.That(ctx.CombatPower.Blocks[0].Element, Is.EqualTo(Element.Fire));
        }

        [Test]
        public void TokensAreSpentBeforeCrystalsAndOneActivationExhaustsUnit()
        {
            var player = new PlayerState(); var ctx = new ActionContext { InBattle = true };
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_003")); player.Units.Add(unit);
            player.Mana.AddToken(ManaColor.Red); player.Mana.AddCrystal(ManaColor.Red);
            CardUnitActions.Use(player, ctx, unit, 1);
            Assert.That(ctx.RangedPool, Is.EqualTo(6)); Assert.That(ctx.CombatPower.Attacks[0].Element, Is.EqualTo(Element.Fire));
            Assert.That(player.Mana.Tokens.ContainsKey(ManaColor.Red), Is.False); Assert.That(player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(player, ctx, unit, 1));
        }

        [Test]
        public void NewRoundReadiesButDoesNotHealSelfWoundedSwordsmen()
        {
            var player = new PlayerState(); var ctx = new ActionContext { InBattle = true };
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_014")); player.Units.Add(unit);
            CardUnitActions.Use(player, ctx, unit, 1, 1);
            Assert.That(ctx.BlockPool, Is.EqualTo(6)); Assert.That(unit.Wounds, Is.EqualTo(1));
            new PlayerTurnEngine().StartTurn(player, DayPart.Day); new TurnEngine().StartTurn(player, 5);
            Assert.That(unit.IsReady, Is.False);
            new GameEngine(new[] { player }, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(unit.IsReady, Is.True); Assert.That(unit.CanActivate, Is.False);
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(player, ctx, unit, 0));
            unit.Heal(1); Assert.That(unit.CanActivate, Is.True);
        }

        [Test]
        public void RecruitmentAtomicallyConsumesRealOfferAndInfluence()
        {
            var player = new PlayerState(); var ctx = new ActionContext { InfluencePool = 3 };
            var offer = new List<UnitCard> { CardJsonLoader.LoadUnit("army_2_006") };
            Assert.That(CardUnitActions.TryRecruit(player, ctx, offer, 0, RecruitLocation.Village), Is.False);
            Assert.That(offer.Count, Is.EqualTo(1)); Assert.That(player.Units.Count, Is.Zero); Assert.That(ctx.InfluencePool, Is.EqualTo(3));
            ctx.InfluencePool = 4;
            Assert.That(CardUnitActions.TryRecruit(player, ctx, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(offer.Count, Is.Zero); Assert.That(ctx.InfluencePool, Is.Zero); Assert.That(player.Units[0].CanActivate, Is.True);
        }

        [Test]
        public void GolemRetainsPrintedPhysicalResistance()
            => Assert.That(CardJsonLoader.LoadUnit("army_2_001").Abilities, Does.Contain(Ability.PhysicalResist));
    }
}
