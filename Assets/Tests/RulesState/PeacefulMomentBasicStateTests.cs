using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.CardEffects;
using NUnit.Framework;
using Monster = MK.Logic.Data.Monster;

namespace MK.Tests.Cards
{
    // Original printed effects consumed through the production turn and unit APIs.
    public sealed class PeacefulMomentBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static DeedCard Add(PlayerState p, string id)
        {
            var c = new DeedCard(id, CardType.Action); p.Deck.Hand.Add(c); return c;
        }
        private static void Play(PlayerState p, ActionContext c, DeedCard card, bool strong = false) =>
            CardPlayActions.Play(p, c, card, (card.Id.StartsWith("advanced_")
                ? CardJsonLoader.LoadAdvancedActions() : CardJsonLoader.LoadBasicActions())
                .Single(x => x.Id == card.Id), strong);
        private static void End(PlayerState p, ActionContext c)
        {
            c.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(p, c.DayPart,
                new ManaSource(1, () => c.DayPart, new Random(35)), c);
            Assert.That(c.PeacefulMomentAvailable, Is.False);
            Assert.That(c.PeacefulMomentReady, Is.False);
            Assert.That(c.PeacefulMomentAction, Is.False);
            Assert.That(c.HealInfluenceRate, Is.Zero);
            Assert.That(c.InfluencePool, Is.Zero);
            Assert.That(c.Healing.Total, Is.Zero);
        }
        private static void OriginalDiscard(PlayerState p, params DeedCard[] cards)
        {
            Assert.That(p.Deck.DiscardPile.Count, Is.EqualTo(cards.Length));
            foreach (var card in cards)
                Assert.That(p.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(p.Deck.PlayedCards, Is.Empty);
        }

        [Test]
        public void BaseTwoRealHealingsCanContinueWithOriginalPromiseAndExcludeRecruitment()
        {
            var p = new PlayerState { Wounds = 2 }; var c = new ActionContext();
            var oldWounds = p.Deck.Hand.ToArray();
            Assert.That(oldWounds.Length, Is.EqualTo(2));
            var peace = Add(p, "advanced_card_035"); var promise = Add(p, "basic_card_008");
            Play(p, c, peace);
            Assert.That(c.InfluencePool, Is.EqualTo(3));
            Assert.That(c.PeacefulMomentAvailable, Is.True);
            Assert.That(c.PeacefulMomentReady, Is.False);
            Assert.That(c.TurnActionCommitted, Is.False);
            PeacefulMomentActions.ConvertHealing(p, c, 1);
            Assert.That(c.InfluencePool, Is.EqualTo(1));
            Assert.That(c.Healing.Total, Is.EqualTo(1));
            Assert.That(c.PeacefulMomentAction, Is.True);
            Assert.That(c.TurnActionCommitted, Is.True);
            CardHealing.Spend(p, c, 1);
            Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(c.Healing.Total, Is.Zero);
            Assert.Throws<InvalidOperationException>(() =>
                RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village));
            var battleGame = new GameState(); battleGame.Players.Add(p);
            var beforePhase = battleGame.CurrentPhase;
            var battle = new TurnMachine(battleGame);
            Assert.Throws<InvalidOperationException>(() => battle.BeginBattle(c));
            Assert.That(battleGame.CurrentPhase, Is.EqualTo(beforePhase));
            Assert.That(c.InBattle, Is.False);
            Assert.That(p.Wounds, Is.EqualTo(1));
            Assert.That(c.InfluencePool, Is.EqualTo(1));
            Play(p, c, promise);
            Assert.That(c.InfluencePool, Is.EqualTo(3));
            PeacefulMomentActions.ConvertHealing(p, c, 1); CardHealing.Spend(p, c, 1);
            Assert.That(c.InfluencePool, Is.EqualTo(1)); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(c.HealedHandWoundsThisTurn, Is.EqualTo(2));
            Assert.That(p.Wounds, Is.Zero); Assert.That(p.Deck.Hand, Is.Empty);
            Assert.That(p.Deck.DiscardPile, Is.Empty);
            foreach (var wound in oldWounds)
                Assert.That(p.Deck.Hand.Concat(p.Deck.PlayedCards).Concat(p.Deck.DiscardPile)
                    .Any(x => ReferenceEquals(x, wound)), Is.False);
            Assert.That(p.Deck.PlayedCards, Is.EqualTo(new[] { peace, promise }));
            End(p, c); OriginalDiscard(p, peace, promise);
        }

        [Test]
        public void StrongWhitePaysHealingAndLevelThreeReadyThenSameUnitAttacksNextTurn()
        {
            var p = new PlayerState { Wounds = 1 }; var c = new ActionContext();
            var oldWound = p.Deck.Hand.Single();
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_003"));
            unit.Exhaust(); p.Units.Add(unit);
            Assert.That(unit.Card.Level, Is.EqualTo(3)); Assert.That(unit.Wounds, Is.Zero);
            p.Mana.AddToken(ManaColor.White);
            var peace = Add(p, "advanced_card_035"); var promise = Add(p, "basic_card_008");
            Play(p, c, peace, true);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(c.InfluencePool, Is.EqualTo(6));
            Assert.That(c.PeacefulMomentReady, Is.True);
            Assert.That(c.TurnActionCommitted, Is.False);
            Play(p, c, promise); Assert.That(c.InfluencePool, Is.EqualTo(8));
            PeacefulMomentActions.ConvertHealing(p, c, 1); CardHealing.Spend(p, c, 1);
            Assert.That(c.InfluencePool, Is.EqualTo(6)); Assert.That(p.Wounds, Is.Zero);
            Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(CardUnitActions.TryReady(p, c, unit, peacefulMoment: true), Is.True);
            Assert.That(c.InfluencePool, Is.Zero); Assert.That(unit.IsReady, Is.True);
            Assert.That(p.Units.Single(), Is.SameAs(unit));
            Assert.That(c.PeacefulMomentAction, Is.True); Assert.That(c.TurnActionCommitted, Is.True);
            End(p, c); OriginalDiscard(p, peace, promise);
            Assert.That(unit.IsReady, Is.True);
            new PlayerTurnEngine().StartTurn(p, c.DayPart);
            Assert.That(c.TurnActionCommitted, Is.False);
            var game = new GameState(); game.Players.Add(p);
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_027");
            Assert.That(enemy.Attack, Is.EqualTo(6)); Assert.That(enemy.Armor, Is.EqualTo(5));
            var enemies = new List<Monster> { enemy }; c.Enemies = enemies;
            var turn = new TurnMachine(game); turn.BeginBattle(c);
            turn.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), c);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), c);
            Assert.That(p.Wounds, Is.EqualTo(3)); Assert.That(c.BattleWoundsReceived, Is.EqualTo(3));
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, oldWound)), Is.False);
            CardUnitActions.Use(p, c, unit, 0, 0);
            Assert.That(unit.IsReady, Is.False); Assert.That(unit.Wounds, Is.Zero);
            Assert.That(c.MeleePool, Is.EqualTo(5));
            var attack = turn.Attack(new[] { 0 }, c);
            Assert.That(attack.PrintedPower, Is.EqualTo(5));
            Assert.That(attack.EffectivePower, Is.EqualTo(5));
            Assert.That(attack.RequiredPower, Is.EqualTo(5));
            Assert.That(attack.Kills, Is.EqualTo(1)); Assert.That(p.Fame, Is.EqualTo(4));
            turn.FinishBattle(c); End(p, c); OriginalDiscard(p, peace, promise);
            Assert.That(p.Units.Single(), Is.SameAs(unit)); Assert.That(unit.IsReady, Is.False);
            Assert.That(p.Deck.Hand.Count, Is.EqualTo(3));
            Assert.That(p.Deck.Hand.All(x => x.Type == CardType.Wound), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryInfluenceStillRecruitsOriginalPeasantsAndCannotAlsoStartHealing(bool strong)
        {
            var p = new PlayerState(); var c = new ActionContext();
            var peace = Add(p, "advanced_card_035");
            DeedCard promise = null;
            if (strong) p.Mana.AddToken(ManaColor.White);
            else promise = Add(p, "basic_card_008");
            Play(p, c, peace, strong);
            if (!strong) Play(p, c, promise);
            Assert.That(c.InfluencePool, Is.EqualTo(strong ? 6 : 5));
            Assert.That(c.TurnActionCommitted, Is.False); Assert.That(c.PeacefulMomentAction, Is.False);
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            var offer = new List<UnitCard> { candidate }; c.UnitOffer = offer;
            Assert.That(candidate.InfluenceCost, Is.EqualTo(4));
            RecruitmentService.BeginInteraction(p, c, RecruitLocation.Village);
            Assert.Throws<InvalidOperationException>(() => PeacefulMomentActions.ConvertHealing(p, c, 1));
            Assert.That(c.InfluencePool, Is.EqualTo(strong ? 6 : 5));
            Assert.That(c.Healing.Total, Is.Zero); Assert.That(c.PeacefulMomentAction, Is.False);
            Assert.That(CardUnitActions.TryRecruit(p, c, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(c.InfluencePool, Is.EqualTo(strong ? 2 : 1));
            Assert.That(offer, Is.Empty); Assert.That(p.Units.Single().Card, Is.SameAs(candidate));
            Assert.That(p.Units.Single().IsReady, Is.True); Assert.That(p.Reputation, Is.Zero);
            Assert.That(p.Mana.Tokens.Values.Sum(), Is.Zero);
            End(p, c); OriginalDiscard(p, strong ? new[] { peace } : new[] { peace, promise });
        }

        [Test]
        public void NextTurnPromiseCannotReuseExpiredHealingOrAffordableReadiness()
        {
            var p = new PlayerState { Wounds = 2 }; var c = new ActionContext();
            var unit = new UnitState(CardJsonLoader.LoadUnit("army_2_006"));
            unit.Exhaust(); p.Units.Add(unit);
            p.Mana.AddToken(ManaColor.White);
            var peace = Add(p, "advanced_card_035"); var promise = Add(p, "basic_card_008");
            Play(p, c, peace, true);
            PeacefulMomentActions.ConvertHealing(p, c, 1); CardHealing.Spend(p, c, 1);
            Assert.That(p.Wounds, Is.EqualTo(1));
            End(p, c); new PlayerTurnEngine().StartTurn(p, c.DayPart);
            Assert.That(p.Deck.Hand.Any(x => ReferenceEquals(x, promise)), Is.True);
            Play(p, c, promise); Assert.That(c.InfluencePool, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => PeacefulMomentActions.ConvertHealing(p, c, 1));
            Assert.That(CardUnitActions.TryReady(p, c, unit, peacefulMoment: true), Is.False);
            Assert.That(c.InfluencePool, Is.EqualTo(2)); Assert.That(c.Healing.Total, Is.Zero);
            Assert.That(p.Wounds, Is.EqualTo(1)); Assert.That(unit.IsReady, Is.False);
            Assert.That(c.TurnActionCommitted, Is.False);
            End(p, c); OriginalDiscard(p, peace, promise);
        }
    }
}
