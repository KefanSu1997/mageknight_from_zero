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

namespace MK.Tests.Cards
{
    public sealed class GemBagStateTests
    {
        private sealed class Rolls : Random
        {
            private readonly Queue<int> _faces;
            public int Calls { get; private set; }
            public Rolls(params ManaColor[] faces) => _faces = new Queue<int>(faces.Select(f => (int)f));
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(6), "Printed card dice use all six faces at either time.");
                Calls++;
                return _faces.Dequeue();
            }
        }

        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState p, ActionContext c, bool burn = false, int option = 0)
        {
            var card = new DeedCard("items_011", CardType.Artifact);
            p.Deck.Hand.Add(card);
            CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == card.Id), burn, option);
            return card;
        }

        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)]
        [TestCase(ManaColor.White)] [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)]
        public void EachFaceIsRolledTwiceAtBothTimes(ManaColor face)
        {
            foreach (var part in new[] { DayPart.Day, DayPart.Night })
            {
                var p = new PlayerState(); var rolls = new Rolls(face, face);
                var c = new ActionContext { DayPart = part, EffectRandom = rolls };
                new GemBagEffect(false, rolls).Execute(p, c, 3 | (3 << 8));
                Assert.That(rolls.Calls, Is.EqualTo(2));
                Assert.That(c.LastManaRolls, Is.EqualTo(new[] { face, face }));
                Assert.That(p.Fame, Is.EqualTo(face == ManaColor.Black ? 2 : 0));
                Assert.That(p.Mana.Crystals.Values.Sum(), Is.EqualTo(face == ManaColor.Black ? 0 : 2));
                if (face != ManaColor.Black)
                    Assert.That(p.Mana.Crystals[face == ManaColor.Gold ? ManaColor.White : face], Is.EqualTo(2));
                Assert.That(p.Mana.Tokens, Is.Empty);
            }
        }

        [Test]
        public void GoldChoicesAreIndependentAndFullInventoryOverflowsToToken()
        {
            var p = new PlayerState(); p.Mana.AddCrystal(ManaColor.Red, 3);
            var c = new ActionContext { DayPart = DayPart.Night, EffectRandom = new Rolls(ManaColor.Gold, ManaColor.Gold) };
            Play(p, c, option: 0 | (2 << 8));
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(3));
            Assert.That(p.Mana.Crystals[ManaColor.Green], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1));
            Assert.That(p.Fame, Is.Zero);
        }

        [Test]
        public void BaseUsesExactInstanceAndKeepsCrystalsAtTurnEnd()
        {
            var p = new PlayerState(); var c = new ActionContext { EffectRandom = new Rolls(ManaColor.Blue, ManaColor.Black) };
            var card = Play(p, c);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == card.Id)));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(1));
        }

        [TestCase(DayPart.Day, ManaColor.Gold)] [TestCase(DayPart.Night, ManaColor.Black)]
        public void BurnGrantsFiveTokensWithoutRollingAndRemovesExactCard(DayPart part, ManaColor extra)
        {
            var p = new PlayerState(); var rolls = new Rolls();
            var c = new ActionContext { DayPart = part, EffectRandom = rolls };
            var card = Play(p, c, true);
            Assert.That(rolls.Calls, Is.Zero);
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White, extra })
                Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(5));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(card));
            Assert.That(p.Mana.Crystals, Is.Empty); Assert.That(p.Fame, Is.Zero);
            new PlayerTurnEngine().EndTurn(p, part, new ManaSource(1, () => part), c);
            Assert.That(p.Mana.Tokens, Is.Empty); Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(card));
        }

        [TestCase(-1)] [TestCase(4)] [TestCase(4 << 8)] [TestCase(1 << 16)]
        public void InvalidChoicesRejectBeforeRollingOrMovingCard(int option)
        {
            var p = new PlayerState(); var rolls = new Rolls(ManaColor.Gold, ManaColor.Gold);
            var c = new ActionContext { EffectRandom = rolls };
            Assert.Throws<InvalidOperationException>(() => Play(p, c, option: option));
            Assert.That(rolls.Calls, Is.Zero); Assert.That(p.Deck.Hand.Count, Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Mana.Crystals, Is.Empty);
            Assert.That(p.Fame, Is.Zero);
        }

        [TestCase(DayPart.Day)] [TestCase(DayPart.Night)]
        public void BurnTokensPayRealOriginalCardCosts(DayPart part)
        {
            var p = new PlayerState(); var c = new ActionContext { DayPart = part, EffectRandom = new Rolls() };
            Play(p, c, true);
            ActionCardData original;
            if (part == DayPart.Day) original = CardJsonLoader.LoadBasicActions().Single(x => x.Id == "basic_card_021");
            else
            {
                var spell = CardJsonLoader.LoadSpells().Single(x => x.Id == "magic_015");
                original = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath, spell.EnImagePath,
                    spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            }
            var instance = new DeedCard(original.Id, part == DayPart.Day ? CardType.Action : CardType.Spell);
            p.Deck.Hand.Add(instance);
            CardPlayActions.Play(p, c, instance, original, true);
            Assert.That(c.MeleePool, Is.EqualTo(part == DayPart.Day ? 4 : 0));
            Assert.That(c.SiegePool, Is.EqualTo(part == DayPart.Night ? 8 : 0));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(part == DayPart.Day ? ManaColor.Red : ManaColor.Blue), Is.Zero);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.EqualTo(part == DayPart.Day ? 4 : 3));
            Assert.That(p.Wounds, Is.EqualTo(part == DayPart.Day ? 0 : 1));
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(instance));
        }
    }
}
