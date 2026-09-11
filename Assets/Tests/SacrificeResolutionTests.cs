using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MageKnight.Tests
{
    public sealed class SacrificeResolutionTests
    {
        [SetUp] public void SetUp() => CardJsonLoader.SetBaseDirectory(Path.Combine(Application.dataPath, "../resources/text_json"));

        private static void Play(PlayerState player, ActionContext ctx, bool strong, int option)
        {
            var card = AssetDatabase.LoadAssetAtPath<SpellCardSO>("Assets/GameData/CardsAssets/magic_011.asset");
            var data = new ActionCardData(card.Id, card.NameCn, card.Set, card.ImagePath, card.EnImagePath,
                card.Top.Effect, card.Bottom.Effect, strong ? card.Bottom.ManaCost : card.Top.ManaCost);
            new ActionSystem().Play(data, player, ctx, strong, option);
        }

        [TestCase(ManaColor.Green, ManaColor.Red, AttackType.Siege, Element.Fire)]
        [TestCase(ManaColor.Green, ManaColor.Blue, AttackType.Siege, Element.Ice)]
        [TestCase(ManaColor.White, ManaColor.Red, AttackType.Ranged, Element.Fire)]
        [TestCase(ManaColor.White, ManaColor.Blue, AttackType.Ranged, Element.Ice)]
        public void OriginalPaidSpellConvertsEveryPairToTypedPowerAndTokens(ManaColor first, ManaColor second, AttackType type, Element element)
        {
            foreach (int pairs in new[] { 0, 1, 2, 3 })
            {
                var p = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night };
                p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Black);
                p.Mana.AddCrystal(first, pairs); p.Mana.AddCrystal(second, 3);
                Play(p, ctx, true, (int)first | ((int)second << 8));
                Assert.That(p.Mana.Crystals.GetValueOrDefault(first), Is.Zero);
                Assert.That(p.Mana.Crystals.GetValueOrDefault(second), Is.EqualTo(3 - pairs));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(first), Is.EqualTo(pairs));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(second), Is.EqualTo(pairs));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
                if (pairs == 0) Assert.That(ctx.CombatPower.Attacks, Is.Empty);
                else Assert.That(ctx.CombatPower.Attacks.Single(), Is.EqualTo(new AttackProfile(type, (first == ManaColor.Green ? 4 : 6) * pairs, element)));
            }
        }

        [Test]
        public void BasicDiscardKeepsActualZonesAndEachOriginalColor()
        {
            var p = new PlayerState(); var ctx = new ActionContext();
            var red = new DeedCard("basic_card_021", CardType.Action); var blue = new DeedCard("basic_card_014", CardType.Action);
            p.Deck.Hand.AddRange(new[] { red, blue }); ctx.CardsToDiscard.AddRange(p.Deck.Hand);
            p.Mana.AddToken(ManaColor.Red); Play(p, ctx, false, 0);
            Assert.That(p.Deck.Hand, Is.Empty); Assert.That(p.Deck.DiscardPile, Has.Count.EqualTo(2));
            Assert.That(p.Deck.DiscardPile[0], Is.SameAs(blue)); Assert.That(p.Deck.DiscardPile[1], Is.SameAs(red));
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(2)); Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(ctx.CardsToDiscard, Is.Empty);
        }

        [Test]
        public void EqualIdCopiesAreDistinctLegalTargetsButForeignCloneIsNotOwned()
        {
            var p = new PlayerState(); var ctx = new ActionContext();
            var one = new DeedCard("basic_card_014", CardType.Action); var two = new DeedCard(one.Id, one.Type);
            p.Deck.Hand.AddRange(new[] { one, two }); ctx.CardsToDiscard.Add(two);
            new SacrificeEffect(false).Execute(p, ctx);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(one)); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(two));
            ctx.CardsToDiscard.Add(new DeedCard(one.Id, one.Type));
            Assert.Throws<InvalidOperationException>(() => new SacrificeEffect(false).Execute(p, ctx));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(one));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ArtifactChoiceCanProduceEachLegalCrystal(int color)
        {
            var p = new PlayerState(); var ctx = new ActionContext(); var artifact = new DeedCard("items_004", CardType.Artifact);
            p.Deck.Hand.Add(artifact); ctx.CardsToDiscard.Add(artifact);
            new SacrificeEffect(false).Execute(p, ctx, color);
            Assert.That(p.Mana.Crystals[(ManaColor)color], Is.EqualTo(color == 0 ? 2 : 1));
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(artifact));
        }

        [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(514)] [TestCase(65538)]
        public void InvalidPairRejectsBeforeAnyPayment(int option)
        {
            var p = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night };
            p.Mana.AddToken(ManaColor.Red); p.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, true, option));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Black], Is.EqualTo(1));
            Assert.That(ctx.CombatPower.Attacks, Is.Empty);
        }

        [Test]
        public void WoundInSelectionRejectsBeforeDiscardAndSpellCost()
        {
            var p = new PlayerState(); var ctx = new ActionContext(); var card = new DeedCard("basic_card_014", CardType.Action);
            var wound = new DeedCard("wound", CardType.Wound); p.Deck.Hand.AddRange(new[] { card, wound });
            ctx.CardsToDiscard.AddRange(p.Deck.Hand); p.Mana.AddToken(ManaColor.Red);
            Assert.Throws<InvalidOperationException>(() => Play(p, ctx, false, 0));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(2)); Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Crystals, Is.Empty);
        }

        [Test]
        public void SpellCostPaidFromCrystalIsRemovedBeforeCountingPairs()
        {
            var p = new PlayerState(); var ctx = new ActionContext { DayPart = DayPart.Night };
            p.Mana.AddToken(ManaColor.Black); p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Green);
            Play(p, ctx, true, 2);
            Assert.That(ctx.CombatPower.Attacks, Is.Empty); Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(1)); Assert.That(p.Mana.Tokens, Is.Empty);
        }
    }
}
