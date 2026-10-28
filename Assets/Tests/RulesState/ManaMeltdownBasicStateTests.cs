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
    public sealed class ManaMeltdownBasicStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState player, ActionContext context, bool strong, int option = 0)
        {
            var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_006");
            var card = new DeedCard(spell.Id, CardType.Spell);
            player.Deck.Hand.Add(card);
            CardPlayActions.Play(player, context, card,
                new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                    spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost),
                strong, option);
            return card;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BaseResolvesEveryPlayerThenChoosesAnActuallyLostCrystal(bool decline)
        {
            var player = new PlayerState();
            player.Mana.AddToken(ManaColor.Red);
            var blue = new PlayerState(); blue.Mana.AddCrystal(ManaColor.Blue);
            var green = new PlayerState(); green.Mana.AddCrystal(ManaColor.Green);
            var empty = new PlayerState();
            var context = new ActionContext { DayPart = DayPart.Day, EffectRandom = new Random(1) };
            context.OtherPlayers.AddRange(new[] { blue, green, empty });

            var source = Play(player, context, false);

            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(1));
            Assert.That(empty.Wounds, Is.EqualTo(1));
            Assert.That(blue.Wounds + green.Wounds, Is.Zero);
            Assert.That(blue.Mana.Crystals.Values.Sum() + green.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(context.ManaMeltdownChoices, Is.EquivalentTo(new[] { ManaColor.Blue, ManaColor.Green }));
            Assert.That(context.ManaMeltdownOwner, Is.SameAs(player));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));

            ManaMeltdownEffect.ChooseCrystal(player, context, decline ? null : ManaColor.Green);

            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(decline ? 0 : 1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(context.ManaMeltdownOwner, Is.Null);
            Assert.That(context.ManaMeltdownChoices, Is.Empty);
        }

        [Test]
        public void BaseWithOneLostColorCompletesAndTheGainedCrystalCanPayAnOriginalSpell()
        {
            var player = new PlayerState(); player.Mana.AddToken(ManaColor.Red);
            player.Mana.AddCrystal(ManaColor.White);
            var other = new PlayerState(); other.Mana.AddCrystal(ManaColor.Green);
            var context = new ActionContext { DayPart = DayPart.Day };
            context.OtherPlayers.Add(other);
            Play(player, context, false);
            Assert.That(player.Wounds + other.Wounds, Is.Zero);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(context.ManaMeltdownOwner, Is.Null);
            var spell = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_005");
            var card = new DeedCard(spell.Id, CardType.Spell); player.Deck.Hand.Add(card);
            CardPlayActions.Play(player, context, card,
                new ActionCardData(spell.Id, spell.Name, spell.Set, spell.ImagePath,
                    spell.EnImagePath, spell.Top.Effect, spell.Bottom.Effect, spell.Top.ManaCost), false);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.Healing.Total, Is.EqualTo(3));
            Assert.That(player.Deck.PlayedCards.Last(), Is.SameAs(card));
        }

        [TestCase(ManaColor.Red)]
        [TestCase(ManaColor.Blue)]
        [TestCase(ManaColor.Green)]
        [TestCase(ManaColor.White)]
        public void StrongWoundsEachPlayerByTheirOldSelectedColorThenGainsTwoCrystals(ManaColor color)
        {
            var player = new PlayerState();
            player.Mana.AddToken(ManaColor.Red); player.Mana.AddToken(ManaColor.Black);
            player.Mana.AddCrystal(color);
            var other = new PlayerState(); other.Mana.AddCrystal(color, 2);
            var unrelated = new PlayerState();
            unrelated.Mana.AddCrystal(color == ManaColor.Blue ? ManaColor.Green : ManaColor.Blue, 3);
            var context = new ActionContext { DayPart = DayPart.Night };
            context.OtherPlayers.AddRange(new[] { other, unrelated });

            var source = Play(player, context, true, (int)color);

            Assert.That(player.Wounds, Is.EqualTo(1));
            Assert.That(other.Wounds, Is.EqualTo(2));
            Assert.That(unrelated.Wounds, Is.Zero);
            Assert.That(player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(1));
            Assert.That(other.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(3));
            Assert.That(other.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(2));
            Assert.That(unrelated.Mana.Crystals.Values.Sum(), Is.EqualTo(3));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(source));
        }

        [TestCase("day")]
        [TestCase("black")]
        [TestCase("red")]
        public void StrongSpellMustMeetPrintedCostsAndNightBeforeHurtingPlayers(string missing)
        {
            var player = new PlayerState();
            if (missing != "red") player.Mana.AddToken(ManaColor.Red);
            if (missing != "black") player.Mana.AddToken(ManaColor.Black);
            var other = new PlayerState(); other.Mana.AddCrystal(ManaColor.Blue);
            var context = new ActionContext { DayPart = missing == "day" ? DayPart.Day : DayPart.Night };
            context.OtherPlayers.Add(other);
            int tokens = player.Mana.Tokens.Values.Sum();
            Assert.Throws<InvalidOperationException>(() => Play(player, context, true, (int)ManaColor.Blue));
            Assert.That(player.Wounds + other.Wounds, Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(tokens));
            Assert.That(other.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
        }
    }
}
