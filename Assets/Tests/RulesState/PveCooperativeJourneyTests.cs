using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveCooperativeJourneyTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static PlayerState Player(int id)
        {
            var player = new PlayerState { Id = id };
            player.Deck.SetDeck(new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_014", "basic_card_000", "basic_card_014", "basic_card_000" }
                .Select(card => new DeedCard(card, CardType.Action)));
            player.Mana.AddCrystal(ManaColor.Red, 2);
            player.Mana.AddCrystal(ManaColor.Blue, 2);
            return player;
        }

        private static PveJourneySession Journey(PlayerState first, PlayerState second) => new PveJourneySession(
            first, CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
            CardJsonLoader.LoadItems(), companions: new[] { second },
            manaRandom: new Random(1), tacticRandom: new Random(1), supplyRandom: new Random(1));

        private static Monster Enemy() => CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_032");
        private static IEnumerable<DeedCard> AllCards(PlayerState player) => player.Deck.Hand
            .Concat(player.Deck.DrawPile).Concat(player.Deck.DiscardPile).Concat(player.Deck.PlayedCards);

        [Test]
        public void ActualPlayersAlternateAndKeepPaidCardsAndRealBattleWounds()
        {
            var first = Player(10); var second = Player(20);
            var firstCards = AllCards(first).ToArray(); var secondCards = AllCards(second).ToArray();
            var journey = Journey(first, second);
            journey.SelectTactics("day_1", "day_4"); journey.BeginTurn();
            Assert.That(journey.Player, Is.SameAs(first));
            var source = journey.Context.ManaSource;
            var battle = journey.BeginBattle(new[] { Enemy() });
            battle.Advance();
            battle.Play(first.Deck.Hand.First(c => c.Id == "basic_card_016"), "strong"); battle.Block(0);
            battle.Advance(); battle.Advance();
            battle.Play(first.Deck.Hand.First(c => c.Id == "basic_card_021"), "strong"); battle.Attack(0); battle.Advance();
            Assert.That(battle.Won, Is.True);
            journey.EndTurn(); journey.BeginTurn();
            Assert.That(journey.Player, Is.SameAs(second));
            Assert.That(journey.Context.ManaSource, Is.SameAs(source));
            Assert.That(first.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            Assert.That(first.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(second.Mana.Crystals[ManaColor.Red], Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => battle.Advance());
            Assert.Throws<InvalidOperationException>(() => battle.Play(first.Deck.Hand.First(), "base"));

            var secondBattle = journey.BeginBattle(new[] { Enemy() });
            while (secondBattle.Phase != Phase.End) secondBattle.Advance();
            var wounds = second.Deck.Hand.Where(c => c.Type == CardType.Wound).ToArray();
            Assert.That(wounds, Is.Not.Empty);
            journey.EndTurn(new[] { second.Deck.Hand.First(c => c.Type != CardType.Wound) });
            journey.BeginTurn();
            Assert.That(journey.Player, Is.SameAs(first));
            Assert.That(first.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            foreach (var wound in wounds) Assert.That(second.Deck.Hand.Any(c => ReferenceEquals(c, wound)), Is.True);
            foreach (var card in firstCards) Assert.That(AllCards(first).Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            foreach (var card in secondCards) Assert.That(AllCards(second).Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(AllCards(first).Any(c => secondCards.Any(other => ReferenceEquals(c, other))), Is.False);
            Assert.That(journey.CompletedTurns, Is.EqualTo(2));
        }

        [Test]
        public void TacticOrderCanStartCompanionAndProgressionFollowsActualPlayer()
        {
            var first = Player(10); var second = Player(20);
            first.HeroId = "Arythea"; second.HeroId = "Braevalar";
            var journey = new PveJourneySession(first, CardJsonLoader.LoadBasicActions(),
                originalSkills: CardJsonLoader.LoadHeroSkills("hero_skill_pool_000"),
                companions: new[] { second }, companionSkills: p => CardJsonLoader.LoadHeroSkills("hero_skill_pool_001"));
            var firstProgression = journey.Progression;
            journey.SelectTactics("day_4", "day_1"); journey.BeginTurn();
            Assert.That(journey.Players[0], Is.SameAs(first)); Assert.That(journey.Players[1], Is.SameAs(second));
            Assert.That(journey.Player, Is.SameAs(second));
            Assert.That(journey.Progression, Is.Not.Null); Assert.That(journey.Progression, Is.Not.SameAs(firstProgression));
            journey.EndTurn(new[] { second.Deck.Hand.First() }); journey.BeginTurn();
            Assert.That(journey.Player, Is.SameAs(first)); Assert.That(journey.Progression, Is.SameAs(firstProgression));
        }

        [Test]
        public void SharedSuppliesExcludeBothPlayersUniqueOwnedCards()
        {
            var first = Player(10); var second = Player(20);
            var actions = CardJsonLoader.LoadAdvancedActions().Where(c => !string.IsNullOrWhiteSpace(c.BaseEffect)).Take(2).ToArray();
            var artifacts = CardJsonLoader.LoadItems().Take(2).ToArray();
            first.Deck.Hand.Add(new DeedCard(actions[0].Id, CardType.Action));
            second.Deck.GainToDiscard(new DeedCard(actions[1].Id, CardType.Action));
            first.Deck.Hand.Add(new DeedCard(artifacts[0].Id, CardType.Artifact));
            second.Deck.GainToDiscard(new DeedCard(artifacts[1].Id, CardType.Artifact));
            var journey = Journey(first, second);
            var advanced = new List<AdvActionCard>();
            while (journey.Game.Globals.AdvActions.Offer.Count > 0) advanced.Add(journey.Game.Globals.AdvActions.Take(0));
            Assert.That(advanced, Is.Not.Empty);
            Assert.That(advanced.Any(c => actions.Any(owned => owned.Id == c.Id)), Is.False);
            Assert.That(advanced.Select(c => c.Id).Distinct().Count(), Is.EqualTo(advanced.Count));
            var remainingArtifacts = new List<ArtifactCard>();
            while (journey.Game.Globals.Artifacts.Count > 0) remainingArtifacts.Add(journey.Game.Globals.Artifacts.Draw());
            Assert.That(remainingArtifacts, Is.Not.Empty);
            Assert.That(remainingArtifacts.Any(c => artifacts.Any(owned => owned.Id == c.Id)), Is.False);
            Assert.That(remainingArtifacts.Select(c => c.Id).Distinct().Count(), Is.EqualTo(remainingArtifacts.Count));
        }

        [TestCase(false)] [TestCase(true)]
        public void DuplicatePlayerIdOrActualReferenceRejectsBeforeStarting(bool sameReference)
        {
            var first = Player(10); var duplicate = sameReference ? first : Player(10);
            var before = first.Deck.DrawPile.ToArray();
            Assert.Throws<ArgumentException>(() => Journey(first, duplicate));
            Assert.That(first.Deck.DrawPile, Is.EqualTo(before));
        }

        [Test]
        public void BattleDropsTravelPointsButKeepsAgilityPermissionForNewActualCard()
        {
            var player = Player(10);
            player.Deck.Hand.Add(new DeedCard("advanced_card_006", CardType.Action));
            player.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            player.Deck.Hand.Add(new DeedCard("basic_card_008", CardType.Action));
            var journey = Journey(player, Player(20));
            journey.SelectTactics("day_1", "day_4"); journey.BeginTurn();
            journey.PlayOutsideBattle(player.Deck.Hand.First(c => c.Id == "advanced_card_006"), "base");
            journey.PlayOutsideBattle(player.Deck.Hand.First(c => c.Id == "basic_card_008"), "base");
            Assert.That(journey.Context.MovementPool, Is.EqualTo(2));
            Assert.That(journey.Context.InfluencePool, Is.EqualTo(2));
            int conversionCost = journey.Context.MoveCostAttack;
            Assert.That(conversionCost, Is.GreaterThan(0));
            var battle = journey.BeginBattle(new[] { Enemy() });
            Assert.That(journey.Context.MovementPool, Is.Zero);
            Assert.That(journey.Context.InfluencePool, Is.Zero);
            Assert.That(journey.Context.MoveCostAttack, Is.EqualTo(conversionCost));
            battle.Advance(); battle.Advance(); battle.Advance();
            Assert.Throws<InvalidOperationException>(() => battle.ConvertMovement(false));
            var move = player.Deck.Hand.First(c => c.Id == "basic_card_000");
            battle.Play(move, "base");
            Assert.That(journey.Context.MovementPool, Is.EqualTo(2));
            battle.ConvertMovement(false, 2 / conversionCost);
            Assert.That(journey.Context.MovementPool, Is.Zero);
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, move)), Is.True);
            var attack = battle.Attack(0);
            Assert.That(attack.PrintedPower, Is.EqualTo(2 / conversionCost));
            Assert.That(journey.Context.MeleePool, Is.Zero);
        }

        [Test]
        public void InvalidEnemySelectionDoesNotDiscardActualTravelResources()
        {
            var player = Player(10);
            player.Deck.Hand.Add(new DeedCard("basic_card_000", CardType.Action));
            player.Deck.Hand.Add(new DeedCard("basic_card_008", CardType.Action));
            var journey = Journey(player, Player(20));
            journey.SelectTactics("day_1", "day_4"); journey.BeginTurn();
            journey.PlayOutsideBattle(player.Deck.Hand.First(c => c.Id == "basic_card_000"), "base");
            journey.PlayOutsideBattle(player.Deck.Hand.First(c => c.Id == "basic_card_008"), "base");
            var enemy = Enemy();
            Assert.Throws<InvalidOperationException>(() => journey.BeginBattle(new[] { enemy, enemy }));
            Assert.That(journey.Context.MovementPool, Is.EqualTo(2));
            Assert.That(journey.Context.InfluencePool, Is.EqualTo(2));
            Assert.That(journey.Context.InBattle, Is.False);
            Assert.That(journey.Context.TurnActionCommitted, Is.False);
            Assert.That(journey.Battle, Is.Null);
        }
    }
}
