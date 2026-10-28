using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class SplitActionManaStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [TestCase(ManaColor.White)]
        [TestCase(ManaColor.Red)]
        public void OriginalExplosiveBoltPaysOnlySelectedHalf(ManaColor color)
        {
            var p = new PlayerState();
            var ctx = new ActionContext { SelectedActionManaColor = color };
            var source = new DeedCard("advanced_card_041", CardType.Action);
            p.Deck.Hand.Add(source);
            var definition = CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == source.Id);
            Assert.That(definition.RequiredCrystals, Is.EquivalentTo(new[] { ManaColor.White, ManaColor.Red }));
            p.Mana.AddToken(color);
            CardPlayActions.Play(p, ctx, source, definition, true);
            Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.That(p.Mana.Crystals, Is.Empty);
            Assert.That(ctx.RangedPool, Is.EqualTo(3));
            Assert.That(ctx.SelectedActionManaColor, Is.Null);
            Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(source));
        }

        [TestCase(null)]
        [TestCase(ManaColor.Blue)]
        public void MissingOrUnprintedSelectionDoesNotPayEitherHalf(ManaColor? selection)
        {
            var p = new PlayerState();
            var ctx = new ActionContext { SelectedActionManaColor = selection };
            var source = new DeedCard("advanced_card_041", CardType.Action);
            p.Deck.Hand.Add(source);
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Red);
            var definition = CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == source.Id);
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.Play(p, ctx, source, definition, true));
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(2));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(source));
            Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(ctx.RangedPool, Is.Zero);
        }
    }
}
