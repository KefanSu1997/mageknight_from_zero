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
    public sealed class MagicTalentStateTests
    {
        [SetUp] public void LoadOriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static void Play(PlayerState p, ActionContext c, bool strong, int option = 0)
            => new ActionSystem().Play(CardJsonLoader.LoadAdvancedActions().Single(d => d.Id == "advanced_card_025"), p, c, strong, option);
        private static ActionContext Context(string spell = "magic_009")
        {
            var c = new ActionContext { SpellSupply = new SpellSupply() };
            c.SpellSupply.Offer.AddRange(new[] { new SpellCard(spell), new SpellCard("magic_015"), new SpellCard("magic_005") });
            return c;
        }
        private static DeedCard AddMaterial(PlayerState p, ActionContext c, string id = "basic_card_021", CardType type = CardType.Action)
        {
            var card = new DeedCard(id, type); p.Deck.Hand.Add(card); c.CardToDiscard = card; return card;
        }

        [TestCase("magic_009", ManaColor.Red)] [TestCase("magic_015", ManaColor.Blue)]
        [TestCase("magic_005", ManaColor.Green)] [TestCase("magic_021", ManaColor.White)]
        public void AcquirePaysBluePlusChosenColorAndGainsRealOfferedSpellWithoutDiscarding(string id, ManaColor color)
        {
            var p = new PlayerState(); var c = Context(id); p.Mana.AddToken(ManaColor.Blue); p.Mana.AddToken(color);
            var hand = AddMaterial(p, c); c.CardToDiscard = null;
            Play(p, c, true, (int)color);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(hand)); Assert.That(p.Deck.DiscardPile.Single().Id, Is.EqualTo(id));
            Assert.That(p.Deck.DiscardPile.Single().Type, Is.EqualTo(CardType.Spell)); Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(2));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero); Assert.That(p.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
        }

        [TestCase("basic_card_021", ManaColor.Red)] [TestCase("basic_card_016", ManaColor.Blue)]
        [TestCase("basic_card_000", ManaColor.Green)] [TestCase("basic_card_008", ManaColor.White)]
        [TestCase("magic_009", ManaColor.Red, CardType.Spell)]
        public void DiscardUsesActualOriginalColor(string id, ManaColor color, CardType type = CardType.Action)
        {
            var p = new PlayerState(); var c = Context(); var card = AddMaterial(p, c, id, type);
            Play(p, c, false, 999);
            Assert.That(p.Deck.Hand, Is.Empty); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(c.SpellColors, Is.EqualTo(new[] { color })); Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateMaterialIdsPreserveTheUnselectedCopy()
        {
            var p = new PlayerState(); var c = Context(); var first = AddMaterial(p, c); var chosen = AddMaterial(p, c);
            Play(p, c, false);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(first)); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(chosen));
        }

        [TestCase("wound", CardType.Wound)] [TestCase("items_000", CardType.Artifact)]
        public void ColorlessCardsCannotGrantPermission(string id, CardType type)
        {
            var p = new PlayerState(); var c = Context(); var card = AddMaterial(p, c, id, type);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, false));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(p.Deck.DiscardPile, Is.Empty); Assert.That(c.SpellColors, Is.Empty);
        }

        [Test]
        public void ForeignMaterialCannotDiscardAnEqualOwnedCopy()
        {
            var p = new PlayerState(); var c = Context(); var card = AddMaterial(p, c);
            c.CardToDiscard = new DeedCard(card.Id, card.Type);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, false));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(c.SpellColors, Is.Empty);
        }

        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)]
        public void MissingAdditionalManaDoesNotPartiallyPayTheBlueCost(ManaColor color)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Blue); var c = Context(color == ManaColor.Blue ? "magic_015" : "magic_009");
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, (int)color));
            Assert.That(p.Mana.Tokens[ ManaColor.Blue ], Is.EqualTo(1)); Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3)); Assert.That(p.Deck.DiscardPile, Is.Empty);
        }

        [TestCase(-1)] [TestCase(768)] [TestCase(4)] [TestCase(1)]
        public void InvalidOfferOrMismatchedColorRejectsBeforeCost(int option)
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.Blue, 2); p.Mana.AddToken(ManaColor.Red); var c = Context();
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, option));
            Assert.That(p.Mana.Tokens[ ManaColor.Blue ], Is.EqualTo(2)); Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1));
            Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3)); Assert.That(p.Deck.DiscardPile, Is.Empty);
        }

        [Test]
        public void OfferedSpellPaysItsCostAndRemainsOfferedButCannotUseOnePermissionTwice()
        {
            var p = new PlayerState(); var c = Context(); var originalOffer = c.SpellSupply.Offer.ToArray(); var discarded = AddMaterial(p, c);
            Play(p, c, false); p.Mana.AddToken(ManaColor.Red, 2);
            CardSpellActions.PlayFromOffer(p, c, 0);
            Assert.That(c.RangedPool, Is.EqualTo(5)); Assert.That(c.CombatPower.Attacks.Single().Element, Is.EqualTo(Element.Fire));
            Assert.That(c.SpellColors, Is.Empty); Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1));
            Assert.That(c.SpellSupply.Offer[0], Is.SameAs(originalOffer[0])); Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3));
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(discarded)); Assert.That(p.Deck.Hand, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => CardSpellActions.PlayFromOffer(p, c, 0));
            Assert.That(c.RangedPool, Is.EqualTo(5)); Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1));
        }

        [Test]
        public void WrongColorInvalidIndexAndMissingCostPreservePermissionThenValidCastSucceeds()
        {
            var p = new PlayerState(); var c = Context(); AddMaterial(p, c); Play(p, c, false);
            Assert.Throws<InvalidOperationException>(() => CardSpellActions.PlayFromOffer(p, c, 1));
            Assert.Throws<InvalidOperationException>(() => CardSpellActions.PlayFromOffer(p, c, 4));
            Assert.Throws<InvalidOperationException>(() => CardSpellActions.PlayFromOffer(p, c, 0));
            Assert.That(c.SpellColors, Is.EqualTo(new[] { ManaColor.Red })); Assert.That(c.RangedPool, Is.Zero);
            p.Mana.AddToken(ManaColor.Red); CardSpellActions.PlayFromOffer(p, c, 0);
            Assert.That(c.RangedPool, Is.EqualTo(5)); Assert.That(c.SpellColors, Is.Empty);
        }

        [TestCase(DayPart.Day, false)] [TestCase(DayPart.Night, true)]
        public void StrongOfferedSpellStillRequiresNightAndBlackMana(DayPart part, bool succeeds)
        {
            var p = new PlayerState(); var c = Context(); c.DayPart = part; AddMaterial(p, c); Play(p, c, false);
            p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Black);
            if (succeeds)
            {
                CardSpellActions.PlayFromOffer(p, c, 0, true);
                Assert.That(c.SiegePool, Is.EqualTo(8)); Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(c.SpellColors, Is.Empty);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => CardSpellActions.PlayFromOffer(p, c, 0, true));
                Assert.That(p.Mana.Tokens[ ManaColor.Red ], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ ManaColor.Black ], Is.EqualTo(1));
                Assert.That(c.SpellColors.Count, Is.EqualTo(1)); Assert.That(p.Wounds, Is.Zero);
            }
            Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3));
        }

        [TestCase(false)] [TestCase(true)]
        public void PermissionExpiresEvenWhenEndTurnSkipsDrawing(bool skip)
        {
            var p = new PlayerState(); var c = Context(); AddMaterial(p, c); Play(p, c, false); c.SkipDraw = skip;
            p.Mana.AddCrystal(ManaColor.Red);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(c.SpellColors, Is.Empty); Assert.That(p.Mana.Crystals[ ManaColor.Red ], Is.EqualTo(1));
            Assert.That(c.SpellSupply.Offer.Count, Is.EqualTo(3));
        }
    }
}
