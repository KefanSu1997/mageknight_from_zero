using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class RefreshingWalkStateTests
    {
        [SetUp]
        public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Play(PlayerState p, ActionContext c, bool strong)
        {
            var card = new DeedCard("advanced_card_007", CardType.Action);
            p.Deck.Hand.Add(card);
            CardPlayActions.Play(p, c, card, CardJsonLoader.LoadAdvancedActions().Single(x => x.Id == card.Id), strong);
            return card;
        }

        private static ActionContext Map()
        {
            var context = new ActionContext { MovementMap = new MapState() };
            foreach (int q in new[] { 0, 1, 2 })
                context.MovementMap.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return context;
        }

        [TestCase(false)] [TestCase(true)]
        public void MovementAndHealingAreIndependentActualConsumers(bool strong)
        {
            var p = new PlayerState { Wounds = 3 }; var c = Map(); p.Mana.AddToken(ManaColor.Green);
            Play(p, c, strong);
            Assert.That(p.Wounds, Is.EqualTo(3)); Assert.That(c.Healing.Total, Is.EqualTo(strong ? 2 : 1));
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.True);
            Assert.That(c.MovementPool, Is.EqualTo(strong ? 2 : 0)); Assert.That(c.Healing.Total, Is.EqualTo(strong ? 2 : 1));
            CardHealing.Spend(p, c, strong ? 2 : 1);
            Assert.That(p.Wounds, Is.EqualTo(strong ? 1 : 2)); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(p.Position.Q, Is.EqualTo(1)); Assert.That(p.Deck.PlayedCards.Count, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalUnitHealingDoesNotSpendMovementOrReadyUnit(bool strong)
        {
            var p = new PlayerState { Wounds = 3 }; var c = Map(); p.Mana.AddToken(ManaColor.Green);
            var unit = new UnitState(CardJsonLoader.LoadUnit(strong ? "army_2_001" : "army_2_006"));
            unit.AddWounds(1); unit.Exhaust(); p.Units.Add(unit);
            Play(p, c, strong); CardHealing.Spend(p, c, unit: unit);
            Assert.That(unit.Wounds, Is.Zero); Assert.That(unit.IsReady, Is.False); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(c.MovementPool, Is.EqualTo(strong ? 4 : 2)); Assert.That(p.Wounds, Is.EqualTo(3));
        }

        [TestCase(false)] [TestCase(true)]
        public void CombatGetsPrintedMoveButCannotMoveAcrossMapOrHeal(bool strong)
        {
            var p = new PlayerState { Wounds = 1 }; var c = Map(); c.InBattle = true; p.Mana.AddToken(ManaColor.Green);
            Play(p, c, strong);
            Assert.That(c.MovementPool, Is.EqualTo(strong ? 4 : 2)); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.False);
            Assert.That(p.Position.Q, Is.Zero); Assert.That(c.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.Throws<InvalidOperationException>(() => CardHealing.Spend(p, c)); Assert.That(p.Wounds, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void EnteringCombatLosesUnspentHealingAndEndingCombatDoesNotRestoreIt(bool strong)
        {
            var p = new PlayerState { Wounds = 1 }; var c = Map(); p.Mana.AddToken(ManaColor.Green);
            Play(p, c, strong); Assert.That(c.Healing.Total, Is.EqualTo(strong ? 2 : 1));
            c.InBattle = true; c.InBattle = false;
            Assert.That(c.Healing.Total, Is.Zero); Assert.Throws<InvalidOperationException>(() => CardHealing.Spend(p, c));
            Assert.That(p.Wounds, Is.EqualTo(1));
        }

        [Test]
        public void AfterActionHealingStillWorksButMovementCannotResume()
        {
            var p = new PlayerState { Wounds = 1 }; var c = Map(); c.TurnActionCommitted = true;
            Play(p, c, false); CardHealing.Spend(p, c);
            Assert.That(p.Wounds, Is.Zero);
            Assert.That(new MovementService().TryMoveUsingPool(p, new AxialCoord(1, 0), c.MovementMap, c), Is.False);
            Assert.That(c.MovementPool, Is.EqualTo(2)); Assert.That(p.Position.Q, Is.Zero);
        }

        [Test]
        public void MissingGreenPreservesBothPoolsAndActualSource()
        {
            var p = new PlayerState { Wounds = 1 }; var c = Map();
            Assert.Throws<InvalidOperationException>(() => Play(p, c, true));
            Assert.That(c.Healing.Total + c.MovementPool, Is.Zero); Assert.That(p.Deck.PlayedCards, Is.Empty);
            Assert.That(p.Wounds, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualSourceDiscardsAndBothPoolsExpireAtEndTurn(bool strong)
        {
            var p = new PlayerState { Wounds = 1 }; var c = Map(); c.SkipDraw = true; p.Mana.AddToken(ManaColor.Green);
            var card = Play(p, c, strong);
            new PlayerTurnEngine().EndTurn(p, DayPart.Day, new ManaSource(1, () => DayPart.Day), c);
            Assert.That(c.Healing.Total + c.MovementPool, Is.Zero); Assert.That(p.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero); Assert.That(p.Wounds, Is.EqualTo(1));
        }
    }
}
