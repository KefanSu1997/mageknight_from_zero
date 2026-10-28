using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RefreshingStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState player, ActionContext context, bool strong, int option)
        {
            var card = new DeedCard("basic_card_005", CardType.Action);
            player.Deck.Hand.Add(card);
            CardPlayActions.Play(player, context, card, CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), strong, option);
            return card;
        }

        [TestCase(false)] [TestCase(true)]
        public void HealingWaitsForChosenOriginalUnit(bool strong)
        {
            var p = new PlayerState { Wounds = 3 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green);
            var unit = new UnitState(CardJsonLoader.LoadUnit(strong ? "army_2_001" : "army_2_006"));
            unit.AddWounds(1); unit.Exhaust(); p.Units.Add(unit);
            Play(p, c, strong, 0);
            Assert.That(p.Wounds, Is.EqualTo(3)); Assert.That(c.Healing.Total, Is.EqualTo(strong ? 2 : 1));
            CardHealing.Spend(p, c, unit: unit);
            Assert.That(unit.Wounds, Is.Zero); Assert.That(unit.IsReady, Is.False); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(p.Fame, Is.Zero); Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void DrawUsesRealDeckAndCannotExceedRemainingCards(bool strong)
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green);
            var draw = new DeedCard("basic_card_000", CardType.Action); p.Deck.PutUnder(draw);
            Play(p, c, strong, 1);
            Assert.That(p.Deck.Hand.Contains(draw), Is.True); Assert.That(p.Deck.DrawPile, Is.Empty);
            Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(c.Healing.Total, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void GreenResourceUsesPrintedTypeAndOverflow(bool strong)
        {
            var p = new PlayerState(); var c = new ActionContext(); p.Mana.AddToken(ManaColor.Green);
            p.Mana.AddCrystal(ManaColor.Green, 3);
            Play(p, c, strong, 2);
            Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(strong ? 1 : 2));
            Assert.That(c.Healing.Total, Is.Zero);
        }

        [TestCase(false, "army_2_001")] [TestCase(true, "army_003")]
        public void ReadyOwnedOriginalUnitDoesNotHeal(bool strong, string id)
        {
            var p = new PlayerState(); var unit = new UnitState(CardJsonLoader.LoadUnit(id));
            unit.AddWounds(1); unit.Exhaust(); p.Units.Add(unit);
            var c = new ActionContext { TargetUnit = unit }; p.Mana.AddToken(ManaColor.Green);
            Play(p, c, strong, 3);
            Assert.That(unit.IsReady, Is.True); Assert.That(unit.Wounds, Is.EqualTo(1));
            Assert.That(unit.CanActivate, Is.False); Assert.That(c.Healing.Total, Is.Zero);
        }

        [TestCase("missing")] [TestCase("foreign")] [TestCase("destroyed")] [TestCase("overlevel")]
        public void IllegalReadyTargetRejectsBeforePayment(string problem)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Green);
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_001")); unit.Exhaust();
            if (problem == "overlevel") unit = new UnitState(CardJsonLoader.LoadUnit("army_003"));
            if (problem != "foreign") p.Units.Add(unit);
            if (problem == "destroyed") unit.Destroy();
            var c = new ActionContext { TargetUnit = problem == "missing" ? null : unit };
            Assert.Throws<InvalidOperationException>(() => Play(p, c, problem != "overlevel", 3));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HandIconRejectsEveryBranchDuringCombatBeforePayment(int option)
        {
            var p = new PlayerState { Wounds = 1 }; p.Mana.AddToken(ManaColor.Green);
            var u = new UnitState(CardJsonLoader.LoadUnit("army_2_001")); u.Exhaust(); p.Units.Add(u);
            var c = new ActionContext { InBattle = true, TargetUnit = u };
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, option));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(1)); Assert.That(u.IsReady, Is.False);
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Wounds, Is.EqualTo(1));
        }

        [TestCase(-1)] [TestCase(4)]
        public void InvalidChoiceRejectsBeforePayment(int option)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Green);
            Assert.Throws<InvalidOperationException>(() => Play(p, new ActionContext(), true, option));
            Assert.That(p.Mana.Tokens[ManaColor.Green], Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void MissingGreenCannotCreateResourceOrApplyAnyBranch(int option)
        {
            var p = new PlayerState { Wounds = 1 }; var u = new UnitState(CardJsonLoader.LoadUnit("army_2_001"));
            u.Exhaust(); p.Units.Add(u); var c = new ActionContext { TargetUnit = u };
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, option));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(u.IsReady, Is.False); Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(c.Healing.Total, Is.Zero);
        }

        [Test]
        public void HealingExpiresAndActualSourceCannotBeReplayed()
        {
            var p = new PlayerState(); var c = new ActionContext { SkipDraw = true };
            var card = Play(p, c, false, 0);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.Play(p, c, card,
                CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id), false, 2));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(c.Healing.Total, Is.Zero); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
        }
    }
}
