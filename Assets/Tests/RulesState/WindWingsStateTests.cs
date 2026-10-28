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
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class WindWingsStateTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static ActionCardData Card()
        {
            var s = CardJsonLoader.LoadSpells().Single(s => s.Id == "magic_023");
            return new ActionCardData(s.Id, s.Name, s.Set, s.ImagePath, s.EnImagePath, s.Top.Effect, s.Bottom.Effect, s.Top.ManaCost);
        }
        private static PlayerState Player()
        {
            var p = new PlayerState(); p.Mana.AddToken(ManaColor.White, 2); p.Mana.AddToken(ManaColor.Black, 2); return p;
        }
        private static ActionContext Context(int steps = 1)
        {
            var c = new ActionContext { DayPart = DayPart.Night, MovementPool = 10, MovementMap = new MapState() };
            for (int q = 0; q <= 5; q++) c.MovementMap.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Forest });
            for (int q = 1; q <= steps; q++) c.TravelPath.Add(new AxialCoord(q, 0));
            c.Enemies = Enumerable.Range(0, 4).Select(i => new Monster("enemy"+i, 4, 4, Element.Physical, 2, Array.Empty<Ability>())).ToList();
            return c;
        }
        private static void Play(PlayerState p, ActionContext c, bool strong, int option)
            => new ActionSystem().Play(Card(), p, c, strong, option);

        [TestCase(1)] [TestCase(5)]
        public void BaseSpendsEachMovePointAndActuallyReachesTheSelectedEndpoint(int steps)
        {
            var p = Player(); var c = Context(steps); Play(p, c, false, steps);
            Assert.That(p.Position.Q, Is.EqualTo(steps)); Assert.That(c.MovementPool, Is.EqualTo(10 - steps));
            Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ ManaColor.Black ], Is.EqualTo(2));
            Assert.That(c.TravelPath, Is.Empty); Assert.That(c.TeleportRange, Is.Zero); Assert.That(c.IgnoreRampaging, Is.False);
        }

        [Test]
        public void IntermediateUnsafeSpaceIsCrossedWithoutProvokingButEndpointMustBeSafe()
        {
            var p = Player(); var c = Context(2);
            c.MovementMap.Places[new AxialCoord(1, 0)] = new PlaceState(PlaceType.OrcRampaging) { Enemy = c.Enemies[0] };
            Play(p, c, false, 2); Assert.That(p.Position.Q, Is.EqualTo(2)); Assert.That(p.Wounds, Is.Zero);
            Assert.That(c.MovementMap.Places[new AxialCoord(1, 0)].Enemy, Is.SameAs(c.Enemies[0]));
        }

        [TestCase(0)] [TestCase(6)] [TestCase(-1)]
        public void InvalidDistanceDoesNotSpendWhiteManaOrMovement(int option)
        {
            var p = Player(); var c = Context(); Assert.Throws<InvalidOperationException>(() => Play(p, c, false, option));
            Assert.That(p.Position.Q, Is.Zero); Assert.That(c.MovementPool, Is.EqualTo(10)); Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidPathUnsafeEndpointOrInsufficientMovementIsAtomic(int failure)
        {
            var p = Player(); var c = Context(2);
            if (failure == 0) c.TravelPath[0] = new AxialCoord(2, 0);
            if (failure == 1) c.MovementMap.Placed.Remove(new AxialCoord(1, 0));
            if (failure == 2) c.MovementMap.Places[new AxialCoord(2, 0)] = new PlaceState(PlaceType.Keep) { Enemy = c.Enemies[0] };
            if (failure == 3) c.MovementPool = 1;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, false, 2));
            Assert.That(c.MovementPool, Is.EqualTo(failure == 3 ? 1 : 10)); Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(2));
            Assert.That(p.Position.Q, Is.Zero); Assert.That(c.TravelPath.Count, Is.EqualTo(2));
        }

        [TestCase(1, 0)] [TestCase(2, 1)] [TestCase(3, 3)] [TestCase(4, 6)]
        public void ExtraTargetsCostOneThenTwoThenThreeAndActuallyDoNotAttack(int count, int cost)
        {
            var p = Player(); var c = Context(); c.InBattle = true; c.SpellTargetIndices.AddRange(Enumerable.Range(0,count).Reverse());
            Play(p, c, true, count);
            Assert.That(c.MovementPool, Is.EqualTo(10 - cost)); Assert.That(c.SkipAttackIndices.OrderBy(i => i), Is.EqualTo(Enumerable.Range(0,count)));
            Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ ManaColor.Black ], Is.EqualTo(1));
            for (int i = 0; i < count; i++) Assert.That(CardCombatActions.Block(p, c, c.Enemies.ToList(), i).Wounds, Is.Zero);
            Assert.That(p.Wounds, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidEnemyDuplicateImmuneOrInsufficientMovementPreservesAllCosts(int failure)
        {
            var p = Player(); var c = Context(); c.InBattle = true;
            c.SpellTargetIndices.AddRange(failure == 0 ? new[] { 0,9 } : failure == 1 ? new[] { 1,1 } : new[] { 0,1 });
            if (failure == 2) c.Enemies = new[] { new Monster("immune", 4, 4, Element.Physical, 2, new[] { Ability.MagicResist }), c.Enemies[1] };
            if (failure == 3) c.MovementPool = 0;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, 2));
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(p.Mana.Tokens[ ManaColor.White ] + p.Mana.Tokens[ ManaColor.Black ], Is.EqualTo(4));
            Assert.That(c.MovementPool, Is.EqualTo(failure == 3 ? 0 : 10));
        }

        [Test]
        public void EndBattleExpiresAllCombatSkipMarkersAndTheirOwnership()
        {
            var p = Player(); var c = Context(); c.InBattle = true; c.SkipAttackIndices.Add(3);
            Play(p, c, true, 1); UnitDamageResolver.EndBattle(p, c);
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.NightWingsSkippedIndices, Is.Empty);
            CardCombatActions.Block(p, c, c.Enemies.ToList(), 0); Assert.That(p.Wounds, Is.GreaterThan(0));
        }

        [TestCase(false)] [TestCase(true)]
        public void NormalSpellManaAndNightRequirementsStillApply(bool missingMana)
        {
            var p = Player(); var c = Context(); c.InBattle = true;
            if (missingMana) p.Mana.Tokens[ ManaColor.Black ] = 0; else c.DayPart = DayPart.Day;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true, 1));
            Assert.That(c.SkipAttackIndices, Is.Empty); Assert.That(c.MovementPool, Is.EqualTo(10)); Assert.That(p.Mana.Tokens[ ManaColor.White ], Is.EqualTo(2));
        }

        [Test]
        public void RealSpellCardChangesZonesWithoutChangingMovementCosts()
        {
            var p = Player(); var c = Context(2); var card = new DeedCard("magic_023", CardType.Spell); p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, Card(), false, 2);
            Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card)); Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(c.MovementPool, Is.EqualTo(8)); Assert.That(p.Position.Q, Is.EqualTo(2));
            new PlayerTurnEngine().EndTurn(p, DayPart.Night, null, c);
            Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card)); Assert.That(p.Deck.PlayedCards, Is.Empty);
        }
    }
}
