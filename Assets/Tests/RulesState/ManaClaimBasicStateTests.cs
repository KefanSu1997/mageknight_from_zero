using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class ManaClaimBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static GameEngine Game(params PlayerState[] players)
        {
            var game = new GameEngine(players, 3, manaRandom: new Random(12012));
            // 正式源的初始化保证基本色占多数；选它实际掷出的颜色，不伪造骰面。
            game.Globals.Mana.ResetSource();
            return game;
        }

        private static ManaDie BasicDie(ManaSource source) => source.Dice.First(d => d.Face <= ManaColor.White);

        private static DeedCard PlaySpell(PlayerState player, ActionContext context, string id,
            bool strong = false, int option = 0)
        {
            var original = CardJsonLoader.LoadSpells().Single(s => s.Id == id);
            var card = new DeedCard(id, CardType.Spell);
            player.Deck.Hand.Add(card);
            CardPlayActions.Play(player, context, card,
                new ActionCardData(original.Id, original.Name, original.Set, original.ImagePath,
                    original.EnImagePath, original.Top.Effect, original.Bottom.Effect, original.Top.ManaCost),
                strong, option);
            return card;
        }

        private static void SpendOnOriginalSpell(PlayerState player, ActionContext context, ManaColor color)
        {
            string id = color switch
            {
                ManaColor.Red => "magic_009",
                ManaColor.Blue => "magic_015",
                ManaColor.Green => "magic_005",
                ManaColor.White => "magic_021",
                _ => throw new InvalidOperationException("测试必须选择实际基本色骰")
            };
            PlaySpell(player, context, id);
        }

        [Test]
        public void ImmediateManaPaysAnOriginalSpellAndTheSameDieStaysUntilRoundEnd()
        {
            var player = new PlayerState(); var game = Game(player); var source = game.Globals.Mana;
            var dice = source.Dice.ToArray(); var die = BasicDie(source); var color = die.Face;
            var context = new ActionContext { ManaSource = source, DayPart = DayPart.Day };
            player.Mana.AddToken(ManaColor.Blue);
            var card = PlaySpell(player, context, "magic_012", option: (int)color);

            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(3));
            Assert.That(source.Dice.Contains(die), Is.False);
            Assert.That(source.RoundClaims.Single().Key, Is.SameAs(die));
            Assert.That(source.RoundClaims[die], Is.SameAs(player));
            Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(card));
            SpendOnOriginalSpell(player, context, color);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(2));

            new PlayerTurnEngine().EndTurn(player, DayPart.Day, source, context);
            Assert.That(source.Dice.Count, Is.EqualTo(dice.Length - 1));
            Assert.That(source.RoundClaims.ContainsKey(die), Is.True);
            Assert.That(player.Mana.Tokens, Is.Empty);
            game.EndRound();
            Assert.That(source.RoundClaims, Is.Empty);
            Assert.That(source.Dice, Is.EquivalentTo(dice));
            Assert.That(source.Dice.Count(d => ReferenceEquals(d, die)), Is.EqualTo(1));
        }

        [Test]
        public void DeferredManaStartsNextTurnAndTheClaimDoesNotOccupyTheOrdinaryDieSlot()
        {
            var player = new PlayerState(); var game = Game(player); var source = game.Globals.Mana;
            var turns = new PlayerTurnEngine(); turns.StartTurn(player, DayPart.Day);
            var ordinary = source.TakeAny(player); var die = BasicDie(source); var color = die.Face;
            var context = new ActionContext { ManaSource = source, DayPart = DayPart.Day };
            player.Mana.AddToken(ManaColor.Blue);
            PlaySpell(player, context, "magic_012", option: 256 + (int)color);

            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
            Assert.That(player.TokensPerTurn[color], Is.EqualTo(1));
            Assert.That(player.HeldManaDie, Is.SameAs(ordinary));
            turns.EndTurn(player, DayPart.Day, source, context);
            Assert.That(source.Dice.Contains(ordinary), Is.True);
            Assert.That(source.Dice.Contains(die), Is.False);
            Assert.That(source.RoundClaims.Single().Key, Is.SameAs(die));
            turns.StartTurn(player, DayPart.Day);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(1));
            turns.EndTurn(player, DayPart.Day, source, context);
            turns.StartTurn(player, DayPart.Day);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(1));
            turns.EndTurn(player, DayPart.Day, source, context);
            game.EndRound();
            turns.StartTurn(player, game.Clock.DayPart);
            Assert.That(player.TokensPerTurn, Is.Empty);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
            Assert.That(source.RoundClaims, Is.Empty);
            Assert.That(source.Dice.Contains(die), Is.True);
        }

        [Test]
        public void CurseCountsTokenAndCrystalSpendingOncePerTurnAndEndsWithTheRound()
        {
            var player = new PlayerState(); var other = new PlayerState(); var game = Game(player, other);
            game.Clock.NextRound(); // 使用正式夜间时钟，不绕过强效法术昼夜限制。
            var source = game.Globals.Mana; var die = BasicDie(source); var color = die.Face;
            var context = new ActionContext { ManaSource = source, DayPart = game.Clock.DayPart };
            context.OtherPlayers.Add(other);
            player.Mana.AddToken(ManaColor.Blue); player.Mana.AddToken(ManaColor.Black);
            PlaySpell(player, context, "magic_012", true, (int)color);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(3));
            Assert.That(other.ManaCurseColor, Is.EqualTo(color));
            Assert.That(player.ManaCurseColor, Is.Null);

            var turns = new PlayerTurnEngine(); var otherContext = new ActionContext { DayPart = game.Clock.DayPart };
            turns.StartTurn(other, game.Clock.DayPart);
            other.Mana.AddToken(color, 2);
            Assert.That(other.Mana.TryPayExactColors(new[] { color, ManaColor.Black }, otherContext, other), Is.False);
            Assert.That(other.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(2));
            Assert.That(other.Wounds, Is.Zero);
            Assert.That(other.ManaCurseTriggered, Is.False);
            SpendOnOriginalSpell(other, otherContext, color);
            Assert.That(other.Wounds, Is.EqualTo(1));
            SpendOnOriginalSpell(other, otherContext, color);
            Assert.That(other.Wounds, Is.EqualTo(1));
            Assert.That(other.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
            turns.EndTurn(other, game.Clock.DayPart, source, otherContext);
            turns.StartTurn(other, game.Clock.DayPart);
            other.Mana.AddCrystal(color);
            SpendOnOriginalSpell(other, otherContext, color);
            Assert.That(other.Wounds, Is.EqualTo(2));
            Assert.That(other.Mana.Crystals.GetValueOrDefault(color), Is.Zero);
            Assert.That(other.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
            var originalWounds = other.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            turns.EndTurn(other, game.Clock.DayPart, source, otherContext);
            game.EndRound();
            Assert.That(other.ManaCurseColor, Is.Null);
            Assert.That(source.RoundClaims, Is.Empty);
            Assert.That(other.Deck.Hand, Is.Empty);
            foreach (var wound in originalWounds)
                Assert.That(other.Deck.DrawPile.Count(c => ReferenceEquals(c, wound)), Is.EqualTo(1));
            turns.StartTurn(other, game.Clock.DayPart); otherContext.DayPart = game.Clock.DayPart;
            var woundsReceived = other.Deck.HandWoundsReceived;
            other.Mana.AddToken(color);
            SpendOnOriginalSpell(other, otherContext, color);
            // The old wounds are in the shuffled deck; paying after expiry must not create a new one.
            Assert.That(other.Wounds, Is.Zero);
            Assert.That(other.Deck.HandWoundsReceived, Is.EqualTo(woundsReceived));
            Assert.That(other.Deck.DrawPile.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
            foreach (var wound in originalWounds)
                Assert.That(other.Deck.DrawPile.Count(c => ReferenceEquals(c, wound)), Is.EqualTo(1));
        }

        [Test]
        public void MissingSelectedDieRejectsBeforePrintedManaPaymentOrCardMovement()
        {
            var player = new PlayerState(); var game = Game(player); var source = game.Globals.Mana;
            var dice = source.Dice.ToArray();
            // 单人三颗源骰，四个基本色中必然至少缺一个；选择真实缺失的颜色。
            var color = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White }
                .First(c => source.GetAvailableCount(c) == 0);
            player.Mana.AddToken(ManaColor.Blue);
            var context = new ActionContext { ManaSource = source, DayPart = DayPart.Day };
            Assert.Throws<InvalidOperationException>(() => PlaySpell(player, context, "magic_012", option: (int)color));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Single().Id, Is.EqualTo("magic_012"));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(source.Dice, Is.EqualTo(dice));
            Assert.That(source.RoundClaims, Is.Empty);
            Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(player.TokensPerTurn, Is.Empty);
        }
    }
}
