using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    // MindReadChoices[player]=actual hand card or null for a verified reveal;
    // MindReadRevealedHands[player]=actual hand snapshot; MindStealTarget=null means decline.
    public sealed class MindReadBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState p, string id)
        {
            var card = new DeedCard(id, id.StartsWith("magic_") ? CardType.Spell : CardType.Action);
            p.Deck.Hand.Add(card); return card;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong = false, int option = 0)
        {
            ActionCardData original;
            if (card.Type == CardType.Spell)
            {
                var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == card.Id);
                original = new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                    spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost);
            }
            else original = CardJsonLoader.LoadBasicActions().Single(x => x.Id == card.Id);
            CardPlayActions.Play(p, c, card, original, strong, option);
        }
        private static ActionContext Context(DayPart part, params PlayerState[] others)
        {
            var c = new ActionContext { DayPart = part, MovementMap = new MapState() };
            c.OtherPlayers.AddRange(others);
            for (int q = 0; q <= 2; q++)
                c.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return c;
        }
        private static DeedCard[] Cards(PlayerState p) => p.Deck.Hand.Concat(p.Deck.PlayedCards)
            .Concat(p.Deck.DiscardPile).Concat(p.Deck.DrawPile).Concat(p.Deck.RemovedCards).ToArray();
        private static void Unique(IReadOnlyList<PlayerState> players, params DeedCard[] originals)
        {
            var all = players.SelectMany(Cards).ToArray();
            Assert.That(all.Length, Is.EqualTo(originals.Length));
            foreach (var card in originals)
                Assert.That(all.Count(x => ReferenceEquals(x, card)), Is.EqualTo(1), card.Id);
        }
        private static void Exact(IEnumerable<DeedCard> actual, params DeedCard[] expected)
        {
            var a = actual.ToArray(); Assert.That(a.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(a[i], Is.SameAs(expected[i]));
        }
        private static void SpendCrystalOnMarch(PlayerState p, ActionContext c, DeedCard march)
        {
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Play(p, c, march, true);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(c.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(c.MovementPool, Is.EqualTo(4));
            for (int q = 1; q <= 2; q++)
            {
                Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(q, 0), c.MovementMap, c), Is.True);
                Assert.That(c.MovementPool, Is.EqualTo(4 - 2 * q));
            }
            Assert.That(p.Position, Is.EqualTo(new AxialCoord(2, 0)));
        }
        private static void End(PlayerState p, ActionContext c)
        {
            c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, c.DayPart, c.ManaSource, c);
        }

        [Test]
        public void BaseDiscardsChosenGreenSpellAndRevealsOtherHandThenCrystalPaysOriginalMarch()
        {
            var p = new PlayerState(); var a = new PlayerState(); var b = new PlayerState();
            var c = Context(DayPart.Day, a, b);
            var source = Add(p, "magic_018"); var march = Add(p, "basic_card_000");
            var aRed = Add(a, "basic_card_021"); var aSpell = Add(a, "magic_005");
            var aMarch = Add(a, "basic_card_000"); var bRed = Add(b, "basic_card_021");
            var bBlue = Add(b, "basic_card_016");
            c.MindReadChoices[a] = aSpell; c.MindReadChoices[b] = null;
            p.Mana.AddToken(ManaColor.White);
            Play(p, c, source, option: (int)ManaColor.Green);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Exact(a.Deck.Hand, aRed, aMarch); Exact(a.Deck.DiscardPile, aSpell);
            Exact(b.Deck.Hand, bRed, bBlue); Assert.That(b.Deck.DiscardPile, Is.Empty);
            Assert.That(c.MindReadRevealedHands.ContainsKey(a), Is.False);
            Exact(c.MindReadRevealedHands[b], bRed, bBlue);
            Exact(p.Deck.PlayedCards, source); Exact(p.Deck.Hand, march);
            SpendCrystalOnMarch(p, c, march); End(p, c);
            Assert.That(p.Deck.DiscardPile, Is.EquivalentTo(new[] { source, march }));
            Unique(new[] { p, a, b }, source, march, aRed, aSpell, aMarch, bRed, bBlue);
        }

        [Test]
        public void StrongExplicitlyStealsSecondPlayersActualMarchAndOwnershipSurvivesRoundEnd()
        {
            var p = new PlayerState(); var a = new PlayerState(); var b = new PlayerState();
            var game = new GameEngine(new[] { p, a, b }, 3, manaRandom: new Random(18));
            game.Clock.NextRound();
            var c = Context(game.Clock.DayPart, a, b); c.ManaSource = game.Globals.Mana;
            var source = Add(p, "magic_018");
            var aRed = Add(a, "basic_card_021"); var aGreen = Add(a, "basic_card_001");
            var bBlue = Add(b, "basic_card_016"); var bMarch = Add(b, "basic_card_000");
            c.MindReadChoices[a] = aGreen; c.MindReadChoices[b] = bMarch;
            c.MindStealTarget = bMarch;
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            Play(p, c, source, true, (int)ManaColor.Green);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Exact(a.Deck.Hand, aRed); Exact(a.Deck.DiscardPile, aGreen);
            Exact(b.Deck.Hand, bBlue); Assert.That(b.Deck.DiscardPile, Is.Empty);
            Exact(p.Deck.Hand, bMarch); Exact(p.Deck.PlayedCards, source);
            Assert.That(c.MindReadRevealedHands, Is.Empty);
            Unique(new[] { p, a, b }, source, aRed, aGreen, bBlue, bMarch);
            SpendCrystalOnMarch(p, c, bMarch); End(p, c);
            Assert.That(p.Deck.DiscardPile, Is.EquivalentTo(new[] { source, bMarch }));
            game.EndRound();
            Assert.That(p.Deck.DrawPile.Any(x => ReferenceEquals(x, bMarch)), Is.True);
            Assert.That(Cards(b).Any(x => ReferenceEquals(x, bMarch)), Is.False);
            Assert.That(a.Deck.Hand, Is.Empty);
            Assert.That(a.Deck.DrawPile.Count, Is.EqualTo(2));
            Assert.That(a.Deck.DrawPile.Count(x => ReferenceEquals(x, aGreen)), Is.EqualTo(1));
            Assert.That(a.Deck.DrawPile.Count(x => ReferenceEquals(x, aRed)), Is.EqualTo(1));
            Unique(new[] { p, a, b }, source, aRed, aGreen, bBlue, bMarch);
        }

        [Test]
        public void StrongMayDeclineStealingAndBothActionAndSpellStayWithOriginalOwners()
        {
            var p = new PlayerState(); var a = new PlayerState(); var b = new PlayerState();
            var c = Context(DayPart.Night, a, b);
            var source = Add(p, "magic_018"); var ownMarch = Add(p, "basic_card_000");
            var aMarch = Add(a, "basic_card_000"); var bSpell = Add(b, "magic_005");
            c.MindReadChoices[a] = aMarch; c.MindReadChoices[b] = bSpell; c.MindStealTarget = null;
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            Play(p, c, source, true, (int)ManaColor.Green);
            Exact(a.Deck.DiscardPile, aMarch); Exact(b.Deck.DiscardPile, bSpell);
            Exact(p.Deck.Hand, ownMarch);
            SpendCrystalOnMarch(p, c, ownMarch); End(p, c);
            Assert.That(p.Deck.DiscardPile, Is.EquivalentTo(new[] { source, ownMarch }));
            Unique(new[] { p, a, b }, source, ownMarch, aMarch, bSpell);
        }

        [Test]
        public void ChoosingDiscardedSpellForTheftRejectsWholeSelectionBeforeFeeOrAnyPlayersDiscard()
        {
            var p = new PlayerState(); var a = new PlayerState(); var b = new PlayerState();
            var c = Context(DayPart.Night, a, b);
            var source = Add(p, "magic_018"); var aMarch = Add(a, "basic_card_000");
            var bSpell = Add(b, "magic_005");
            c.MindReadChoices[a] = aMarch; c.MindReadChoices[b] = bSpell; c.MindStealTarget = bSpell;
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, source, true, (int)ManaColor.Green));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.Zero);
            Exact(p.Deck.Hand, source); Exact(a.Deck.Hand, aMarch); Exact(b.Deck.Hand, bSpell);
            Assert.That(a.Deck.DiscardPile, Is.Empty); Assert.That(b.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(c.MindReadRevealedHands, Is.Empty);
            Unique(new[] { p, a, b }, source, aMarch, bSpell);
        }

        [Test]
        public void SameIdCloneIsRejectedAndOnlyChosenSecondActualInstanceChangesOwner()
        {
            var p = new PlayerState(); var other = new PlayerState();
            var c = Context(DayPart.Night, other);
            var source = Add(p, "magic_018");
            var first = Add(other, "basic_card_000"); var second = Add(other, "basic_card_000");
            var clone = new DeedCard("basic_card_000", CardType.Action);
            c.MindReadChoices[other] = clone; c.MindStealTarget = clone;
            p.Mana.AddToken(ManaColor.White); p.Mana.AddToken(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, source, true, (int)ManaColor.Green));
            Exact(other.Deck.Hand, first, second); Exact(p.Deck.Hand, source);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.EqualTo(1));
            c.MindReadChoices[other] = second; c.MindStealTarget = second;
            Play(p, c, source, true, (int)ManaColor.Green);
            Exact(other.Deck.Hand, first); Exact(p.Deck.Hand, second);
            Exact(p.Deck.PlayedCards, source); Assert.That(other.Deck.DiscardPile, Is.Empty);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Unique(new[] { p, other }, source, first, second);
        }

        [TestCase(ManaColor.Red)]
        [TestCase(ManaColor.Blue)]
        [TestCase(ManaColor.Green)]
        [TestCase(ManaColor.White)]
        public void EachBasicColorSelectionAwardsExactlyThatCrystal(ManaColor color)
        {
            var p = new PlayerState(); var c = Context(DayPart.Day);
            var source = Add(p, "magic_018"); p.Mana.AddToken(ManaColor.White);
            Play(p, c, source, option: (int)color);
            Assert.That(p.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.Values.Sum(), Is.EqualTo(1));
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Exact(p.Deck.PlayedCards, source); Assert.That(p.Deck.Hand, Is.Empty);
        }
    }
}
