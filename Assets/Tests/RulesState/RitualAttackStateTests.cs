using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RitualAttackStateTests
    {
        [SetUp]
        public void LoadOriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static void Play(PlayerState p, ActionContext ctx, bool enhanced, int option = 0)
            => new ActionSystem().Play(CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == "advanced_card_029"), p, ctx, enhanced, option);

        [TestCase("basic_card_021", false, 5, Element.Physical, AttackType.Melee)]
        [TestCase("basic_card_021", true, 6, Element.Fire, AttackType.Melee)]
        [TestCase("basic_card_016", false, 3, Element.Ice, AttackType.Melee)]
        [TestCase("basic_card_016", true, 4, Element.ColdFire, AttackType.Melee)]
        [TestCase("basic_card_008", false, 3, Element.Physical, AttackType.Ranged)]
        [TestCase("basic_card_008", true, 4, Element.Fire, AttackType.Ranged)]
        [TestCase("basic_card_000", false, 2, Element.Physical, AttackType.Siege)]
        [TestCase("basic_card_000", true, 3, Element.Fire, AttackType.Siege)]
        public void OriginalColorProducesTypedAttackAndActualKill(string id, bool enhanced, int amount, Element element, AttackType type)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red);
            var card = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(card);
            var ctx = new ActionContext { CardToRemove = card };
            Play(p, ctx, enhanced, 999); // Legacy option must not counterfeit the selected card's color.
            var attack = ctx.CombatPower.Attacks.Single();
            Assert.That(attack.Value, Is.EqualTo(amount)); Assert.That(attack.Element, Is.EqualTo(element)); Assert.That(attack.Type, Is.EqualTo(type));
            Assert.That(p.Deck.Hand, Is.Empty); Assert.That(p.Deck.DiscardPile, Is.Empty); Assert.That(ctx.CardToRemove, Is.Null);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(enhanced ? 0 : 1));
            var enemy = new Monster("target", amount, 0, Element.Physical, 3, Array.Empty<Ability>());
            var result = CardCombatActions.Attack(p, ctx, new[] { enemy }, new[] { 0 }, type == AttackType.Melee ? Phase.Melee : Phase.Ranged);
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(3)); Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [Test]
        public void DuplicateIdsRemoveTheSelectedInstanceOnly()
        {
            var p = new PlayerState(); var first = new DeedCard("basic_card_021", CardType.Action);
            var selected = new DeedCard(first.Id, first.Type); p.Deck.Hand.Add(first); p.Deck.Hand.Add(selected);
            Play(p, new ActionContext { CardToRemove = selected }, false);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(first)); Assert.That(p.Deck.DiscardPile, Is.Empty);
        }

        [TestCase("magic_000", CardType.Spell)]
        [TestCase("items_000", CardType.Artifact)]
        [TestCase("wound", CardType.Wound)]
        [TestCase("missing_original_card", CardType.Action)]
        public void InvalidCardRejectsBeforeManaOrRemoval(string id, CardType type)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red);
            var card = new DeedCard(id, type); p.Deck.Hand.Add(card); var ctx = new ActionContext { CardToRemove = card };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1));
            Assert.That(ctx.CombatPower.Attacks, Is.Empty); Assert.That(p.Deck.DiscardPile, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void MissingOrForeignInstanceCannotPay(bool foreign)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red); var card = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(card);
            var ctx = new ActionContext { CardToRemove = foreign ? new DeedCard(card.Id, card.Type) : null };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1)); Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [Test]
        public void InsufficientManaPreservesTheRemovalTarget()
        {
            var p = new PlayerState(); var card = new DeedCard("basic_card_021", CardType.Action); p.Deck.Hand.Add(card);
            var ctx = new ActionContext { CardToRemove = card };
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, true));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(ctx.CardToRemove, Is.SameAs(card)); Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [TestCase("basic_card_021", true, Ability.FireResist, 3)]
        [TestCase("basic_card_016", false, Ability.IceResist, 1)]
        [TestCase("basic_card_016", true, Ability.FireResist, 4)]
        [TestCase("basic_card_008", true, Ability.FireResist, 2)]
        [TestCase("basic_card_000", true, Ability.FireResist, 1)]
        public void ActualAttackUsesItsPrintedElementAgainstResistance(string id, bool enhanced, Ability resist, int effective)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red);
            var card = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(card); var ctx = new ActionContext { CardToRemove = card };
            Play(p, ctx, enhanced);
            var phase = ctx.CombatPower.Attacks.Single().Type == AttackType.Melee ? Phase.Melee : Phase.Ranged;
            var result = CardCombatActions.Attack(p, ctx, new[] { new Monster("resistant", 4, 0, Element.Physical, 3, new[] { resist }) }, new[] { 0 }, phase);
            Assert.That(result.EffectivePower, Is.EqualTo(effective)); Assert.That(result.Kills, Is.EqualTo(effective >= 4 ? 1 : 0));
            Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [TestCase(false, false, true)] [TestCase(true, false, true)] [TestCase(true, true, false)]
        public void SiegeRetainsFortificationRules(bool enhanced, bool fortifiedSite, bool allowed)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Red); var card = new DeedCard("basic_card_000", CardType.Action); p.Deck.Hand.Add(card);
            var ctx = new ActionContext { CardToRemove = card, FortifiedSite = fortifiedSite }; Play(p, ctx, enhanced);
            var enemies = new[] { new Monster("fortified", 2, 0, Element.Physical, 3, new[] { Ability.Fortified }) };
            if (allowed)
                Assert.That(CardCombatActions.Attack(p, ctx, enemies, new[] { 0 }, Phase.Ranged).Kills, Is.EqualTo(1));
            else
            {
                Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, ctx, enemies, new[] { 0 }, Phase.Ranged));
                Assert.That(ctx.SiegePool, Is.EqualTo(3)); Assert.That(p.Fame, Is.Zero);
            }
        }
    }
}
