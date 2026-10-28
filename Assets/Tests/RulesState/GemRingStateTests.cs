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
    public sealed class GemRingStateTests
    {
        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard PlayRing(PlayerState p, ActionContext c, string id, bool burn)
        {
            var card = new DeedCard(id, CardType.Artifact);
            p.Deck.Hand.Add(card);
            CardPlayActions.PlayArtifact(p, c, card, CardJsonLoader.LoadItems().Single(x => x.Id == id), burn);
            return card;
        }

        private static DeedCard Spell(PlayerState p, ActionContext c, string id, bool strong = false)
        {
            var original = CardJsonLoader.LoadSpells().Single(x => x.Id == id);
            var card = new DeedCard(id, CardType.Spell);
            p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, new ActionCardData(original.Id, original.Name, original.Set,
                original.ImagePath, original.EnImagePath, original.Top.Effect, original.Bottom.Effect, original.Top.ManaCost), strong);
            return card;
        }

        [TestCase(false)] [TestCase(true)]
        public void EndlessManaAndFameExpireEvenWhenDrawingIsSkipped(bool skip)
        {
            var p = new PlayerState(); var c = new ActionContext { SkipDraw = skip };
            new RubyRingEffect(true).Execute(p, c);
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, new ManaSource(1, () => DayPart.Night), c);
            Assert.That(c.InfiniteMana, Is.Empty);
            Assert.That(c.FamePerSpell, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => Spell(p, c, "magic_009"));
            p.Mana.AddToken(ManaColor.Red);
            Spell(p, c, "magic_009");
            Assert.That(p.Fame, Is.Zero, "Next-turn paid spells cannot collect the old ring reward.");
        }

        [TestCase("items_004", ManaColor.Red)] [TestCase("items_005", ManaColor.Blue)]
        [TestCase("items_006", ManaColor.White)] [TestCase("items_007", ManaColor.Green)]
        public void BaseUsesActualInstanceOnceAndDiscardsAtTurnEnd(string id, ManaColor color)
        {
            var p = new PlayerState(); var c = new ActionContext();
            var card = PlayRing(p, c, id, false);
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.PlayArtifact(p, c, card,
                CardJsonLoader.LoadItems().Single(x => x.Id == id)));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(1));
        }

        [TestCase("items_004", ManaColor.Red)] [TestCase("items_005", ManaColor.Blue)]
        [TestCase("items_006", ManaColor.White)] [TestCase("items_007", ManaColor.Green)]
        public void FullCrystalInventoryConvertsExcessToTemporaryMana(string id, ManaColor color)
        {
            var p = new PlayerState(); var c = new ActionContext();
            p.Mana.AddCrystal(color, 3); PlayRing(p, c, id, false);
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(2));
            Assert.That(p.Fame, Is.EqualTo(1));
        }

        [TestCase("items_004", ManaColor.Red, "magic_009")]
        [TestCase("items_005", ManaColor.Blue, "magic_015")]
        [TestCase("items_006", ManaColor.White, "magic_018")]
        [TestCase("items_007", ManaColor.Green, "magic_001")]
        public void BurnPaysRepeatedMatchingSpellsAndNightBlackWithoutSpendingInventory(string id, ManaColor color, string spell)
        {
            var p = new PlayerState(); var c = new ActionContext { DayPart = DayPart.Night };
            p.Mana.AddToken(color); p.Mana.AddCrystal(color);
            var ring = PlayRing(p, c, id, true);
            var first = Spell(p, c, spell);
            if (spell == "magic_001") c.InBattle = true;
            var second = Spell(p, c, spell, true);
            Assert.That(p.Fame, Is.EqualTo(2));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(1));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(ring));
            Assert.That(p.Deck.PlayedCards, Is.EqualTo(new[] { first, second }));
            Assert.Throws<InvalidOperationException>(() => CardPlayActions.PlayArtifact(p, c, ring,
                CardJsonLoader.LoadItems().Single(x => x.Id == id), true));
        }

        [TestCase("items_004", "magic_009")] [TestCase("items_005", "magic_015")]
        [TestCase("items_006", "magic_018")] [TestCase("items_007", "magic_001")]
        public void EndlessBlackDoesNotPermitStrongSpellsByDay(string id, string spell)
        {
            var p = new PlayerState(); var c = new ActionContext { DayPart = DayPart.Day };
            PlayRing(p, c, id, true);
            Assert.Throws<InvalidOperationException>(() => Spell(p, c, spell, true));
            Assert.That(p.Fame, Is.Zero); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(1)); Assert.That(p.Mana.Tokens, Is.Empty);
        }

        [Test]
        public void DifferentColorSpellStillPaysItsColorAndDoesNotEarnRedReward()
        {
            var p = new PlayerState(); var c = new ActionContext { DayPart = DayPart.Night };
            PlayRing(p, c, "items_004", true);
            Assert.Throws<InvalidOperationException>(() => Spell(p, c, "magic_015", true));
            p.Mana.AddToken(ManaColor.Blue); Spell(p, c, "magic_015", true);
            Assert.That(p.Fame, Is.Zero); Assert.That(p.Mana.Tokens, Is.Empty);
            Assert.That(c.SiegePool, Is.EqualTo(8));
        }

        [Test]
        public void RingPaysMatchingEnhancedActionButOnlySpellsEarnFame()
        {
            var p = new PlayerState(); var c = new ActionContext(); PlayRing(p, c, "items_004", true);
            var original = CardJsonLoader.LoadBasicActions().Single(x => x.Id == "basic_card_021");
            var card = new DeedCard(original.Id, CardType.Action); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, original, true);
            Assert.That(c.MeleePool, Is.EqualTo(4)); Assert.That(p.Fame, Is.Zero);
            Assert.That(p.Mana.Tokens, Is.Empty);
        }
    }
}
