using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    public sealed class TemporalPortalLifecycleTests
    {
        [SetUp] public void OriginalData() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static ActionContext Context()
        {
            var c = new ActionContext { MovementMap = new MapState() };
            for (int q = 0; q < 4; q++) c.MovementMap.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Forest });
            return c;
        }
        private static DeedCard AddPortal(PlayerState p)
        {
            var card = new DeedCard("advanced_card_032", CardType.Action); p.Deck.Hand.Add(card); return card;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong = false, int option = 0)
            => CardPlayActions.Play(p, c, card, CardJsonLoader.LoadAdvancedActions().Single(a => a.Id == card.Id), strong, option);

        [TestCase(false, 0, 1, 6)] [TestCase(true, 0, 2, 6)] [TestCase(true, 1, 1, 7)]
        public void RealSelectedCardMovesHandToPlayedToDiscardAndRefills(bool strong, int option, int distance, int hand)
        {
            var p = new PlayerState(); var c = Context(); var card = AddPortal(p); p.Mana.AddToken(ManaColor.Blue);
            p.Deck.Hand.Add(new DeedCard("basic_card_021", CardType.Action));
            for (int i = 0; i < 10; i++) p.Deck.PutUnder(new DeedCard("basic_card_000", CardType.Action));
            Play(p, c, card, strong, option);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(card)); Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(distance, 0)), Is.True);
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card));
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(hand)); Assert.That(p.Deck.DrawPileCount, Is.EqualTo(11 - hand));
            Assert.That(p.Deck.Hand.Count + p.Deck.DiscardPile.Count + p.Deck.DrawPileCount, Is.EqualTo(12));
            Assert.That(c.TemporalPortalTurn || c.TurnActionCommitted, Is.False);
        }

        [Test]
        public void DuplicateIdsMoveOnlyTheSelectedInstanceAndCannotPerformASecondAction()
        {
            var p = new PlayerState(); var c = Context(); var first = AddPortal(p); var second = AddPortal(p);
            Play(p, c, second); Assert.That(p.Deck.Hand.Single(), Is.SameAs(first)); Assert.That(p.Deck.PlayedCards.Single(), Is.SameAs(second));
            CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0));
            Assert.Throws<InvalidOperationException>(() => Play(p, c, first)); Assert.That(p.Deck.Hand.Single(), Is.SameAs(first));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ForeignCardMissingManaOrInvalidChoiceCannotLeaveHand(int failure)
        {
            var p = new PlayerState(); var c = Context(); var card = AddPortal(p);
            if (failure != 1) p.Mana.AddToken(ManaColor.Blue);
            var selected = failure == 0 ? new DeedCard(card.Id, card.Type) : card;
            Assert.Throws<InvalidOperationException>(() => Play(p, c, selected, true, failure == 2 ? 2 : 0));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(p.Deck.PlayedCards, Is.Empty); Assert.That(p.Deck.DiscardPile, Is.Empty);
            Assert.That(c.TurnActionCommitted, Is.False); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(failure == 1 ? 0 : 1));
        }

        [Test]
        public void PortalActionPreventsNormalMoveAttackBlockAndRecruitmentEvenAfterTeleport()
        {
            var p = new PlayerState(); var c = Context(); Play(p, c, AddPortal(p));
            Assert.That(CardTravelActions.TryTemporalPortal(p, c, new AxialCoord(1, 0)), Is.True);
            c.MovementPool = c.MeleePool = c.BlockPool = c.InfluencePool = 10;
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(2, 0), c.MovementMap, c), Is.False);
            var enemy = new Monster("fixture", 4, 4, Element.Physical, 2, Array.Empty<Ability>());
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Attack(p, c, new[] { enemy }, new[] { 0 }, Phase.Melee));
            Assert.Throws<InvalidOperationException>(() => CardCombatActions.Block(p, c, enemy));
            var offer = new System.Collections.Generic.List<UnitCard> { CardJsonLoader.LoadUnit("army_2_006") };
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.False);
            Assert.That(p.Position.Q, Is.EqualTo(1)); Assert.That(c.MovementPool + c.MeleePool + c.BlockPool + c.InfluencePool, Is.EqualTo(40));
            Assert.That(offer.Count, Is.EqualTo(1)); Assert.That(p.Units, Is.Empty);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, null, c);
            c.MovementPool = 3;
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(2, 0), c.MovementMap, c), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void CombatOrRecruitmentAlreadyUsedTheTurnAction(bool recruit)
        {
            var p = new PlayerState(); var c = Context(); var card = AddPortal(p);
            if (recruit)
            {
                c.InfluencePool = 4;
                var offer = new System.Collections.Generic.List<UnitCard> { CardJsonLoader.LoadUnit("army_2_006") };
                Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            }
            else
            {
                c.MeleePool = 4;
                CardCombatActions.Attack(p, c, new[] { new Monster("fixture", 4, 0, Element.Physical, 2, Array.Empty<Ability>()) }, new[] { 0 }, Phase.Melee);
            }
            Assert.Throws<InvalidOperationException>(() => Play(p, c, card));
            Assert.That(p.Deck.Hand.Single(), Is.SameAs(card)); Assert.That(c.NextDrawBonus, Is.Zero);
        }
    }
}
