using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class WrathHornStateTests
    {
        private sealed class FixedDie : Random
        {
            private readonly ManaColor _face;
            public int Calls { get; private set; }
            public FixedDie(ManaColor face) => _face = face;
            public override int Next(int maxValue)
            {
                Assert.That(maxValue, Is.EqualTo(6));
                Calls++;
                return (int)_face;
            }
        }

        [SetUp]
        public void Setup() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static (PlayerState p, ActionContext c, DeedCard horn, FixedDie die) Fixture(ManaColor face)
        {
            var p = new PlayerState();
            var die = new FixedDie(face);
            var c = new ActionContext { InBattle = true, EffectRandom = die, SkipDraw = true };
            c.Enemies = new System.Collections.Generic.List<Monster>
                { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_017") };
            var horn = new DeedCard("items_009", CardType.Artifact);
            p.Deck.Hand.Add(horn);
            return (p, c, horn, die);
        }

        private static void Play(PlayerState p, ActionContext c, DeedCard horn, bool burn = false, int bonus = 0)
            => CardPlayActions.PlayArtifact(p, c, horn, CardJsonLoader.LoadItems().Single(i => i.Id == "items_009"), burn, bonus);

        [TestCase(ManaColor.Red, 1)]
        [TestCase(ManaColor.Gold, 0)]
        public void ActualBasicHornRollsOnceThenSiegeKillsOriginalFortifiedEnemy(ManaColor face, int wounds)
        {
            var (p, c, horn, die) = Fixture(face);
            p.Mana.AddToken(ManaColor.Red, 1);
            Play(p, c, horn);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(horn));
            Assert.That(p.Deck.RemovedCards.Count, Is.Zero);
            Assert.That(p.Wounds, Is.EqualTo(wounds));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(wounds));
            Assert.That(c.BattleWoundsReceived, Is.EqualTo(wounds));
            Assert.That(c.SiegePool, Is.EqualTo(5));
            Assert.That(c.RangedPool, Is.Zero);
            Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1));
            Assert.That(die.Calls, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, horn));
            Assert.That(die.Calls, Is.EqualTo(1));
            Assert.That(c.SiegePool, Is.EqualTo(5));
            var enemy = c.Enemies.Single();
            Assert.That(enemy.Armor, Is.EqualTo(4));
            Assert.That(enemy.Abilities, Does.Contain(Ability.Fortified));
            var result = CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Ranged);
            Assert.That(result.PrintedPower, Is.EqualTo(5));
            Assert.That(result.EffectivePower, Is.EqualTo(5));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(p.Fame, Is.EqualTo(3));
            Assert.That(c.SiegePool, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Ranged));
            Assert.That(p.Fame, Is.EqualTo(3));
        }

        [Test]
        public void BurnFiveRedRollsKnocksOutAfterActualBurnButExistingSiegeStillWorks()
        {
            var (p, c, horn, die) = Fixture(ManaColor.Red);
            var other = new DeedCard("basic_card_000", CardType.Action);
            p.Deck.Hand.Add(other);
            Play(p, c, horn, true, 5);
            Assert.That(die.Calls, Is.EqualTo(5));
            Assert.That(c.LastManaRolls, Is.EqualTo(Enumerable.Repeat(ManaColor.Red, 5)));
            Assert.That(c.IsKnockedOut, Is.True);
            Assert.That(c.BattleWoundsReceived, Is.EqualTo(5));
            Assert.That(p.Wounds, Is.EqualTo(5));
            Assert.That(p.Deck.Hand.All(card => card.Type == CardType.Wound), Is.True);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(other));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(horn));
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero);
            Assert.That(c.SiegePool, Is.EqualTo(10));
            Assert.That(CardCombatActions.Attack(p, c, c.Enemies, new[] { 0 }, Phase.Ranged).Kills, Is.EqualTo(1));
            Assert.That(c.SiegePool, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(3));
        }

        [Test]
        public void BurnZeroBonusStillSuppliesFiveWithoutRollingOrCreatingWounds()
        {
            var (p, c, horn, die) = Fixture(ManaColor.Black);
            Play(p, c, horn, true, 0);
            Assert.That(c.SiegePool, Is.EqualTo(5));
            Assert.That(die.Calls, Is.Zero);
            Assert.That(c.LastManaRolls.Count, Is.Zero);
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(horn));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, horn, true));
            Assert.That(c.SiegePool, Is.EqualTo(5));
        }

        [TestCase(false, 1)]
        [TestCase(true, -1)]
        [TestCase(true, 6)]
        public void InvalidChoicePreservesRealHandManaAndDice(bool burn, int bonus)
        {
            var (p, c, horn, die) = Fixture(ManaColor.Red);
            p.Mana.AddCrystal(ManaColor.Blue, 1);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, horn, burn, bonus));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(horn));
            Assert.That(p.Deck.PlayedCards.Count + p.Deck.RemovedCards.Count, Is.Zero);
            Assert.That(c.SiegePool, Is.Zero);
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(die.Calls, Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BattleAndRoundLifecyclePreservesActualBaseOrBurnZone(bool burn)
        {
            var (p, c, horn, die) = Fixture(ManaColor.Blue);
            Play(p, c, horn, burn);
            UnitDamageResolver.EndBattle(p, c);
            Assert.That(c.SiegePool, Is.Zero);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Deck.PlayedCards.Count, Is.Zero);
            Assert.That(burn ? p.Deck.RemovedCards.Single() : p.Deck.DiscardPile.Single(), Is.SameAs(horn));
            var game = new GameEngine(new[] { p }, 3);
            game.EndRound();
            game.StartNewRound(Array.Empty<TacticCard>());
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(burn ? 0 : 1));
            if (!burn) Assert.That(p.Deck.Hand.Single(), Is.SameAs(horn));
            else Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(horn));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnownedSameIdHornCannotRollOrGrantAttack(bool burn)
        {
            var (p, c, horn, die) = Fixture(ManaColor.Black);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, new DeedCard("items_009", CardType.Artifact), burn));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(horn));
            Assert.That(die.Calls, Is.Zero);
            Assert.That(c.SiegePool, Is.Zero);
            Assert.That(p.Wounds, Is.Zero);
        }

        [Test]
        public void TwoActualHornsKeepIndependentPlayedAndRemovedIdentity()
        {
            var (p, c, first, die) = Fixture(ManaColor.White);
            var second = new DeedCard("items_009", CardType.Artifact);
            p.Deck.Hand.Add(second);
            Play(p, c, second, true, 2);
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(first));
            Assert.That(p.Deck.RemovedCards.Single(), Is.SameAs(second));
            Play(p, c, first);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(first));
            Assert.That(c.SiegePool, Is.EqualTo(12));
            Assert.That(die.Calls, Is.EqualTo(3));
            Assert.That(p.Wounds, Is.Zero);
        }
    }
}
