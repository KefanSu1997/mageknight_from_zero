using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class BloodSurgeBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id, CardType type = CardType.Action)
        {
            var card = new DeedCard(id, type); player.Deck.Hand.Add(card); return card;
        }

        private static DeedCard[] OrderedDraw(PlayerState player)
        {
            var cards = new[] { "basic_card_021", "basic_card_016", "basic_card_000", "basic_card_008", "basic_card_001" }
                .Select(id => new DeedCard(id, CardType.Action)).ToArray();
            foreach (var card in cards) Assert.That(CardJsonLoader.LoadBasicActions().Any(c => c.Id == card.Id), Is.True);
            player.Deck.SetDeck(cards); // Must precede adding the source and any original hand wound.
            return cards;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card, bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("advanced_card_") ? CardJsonLoader.LoadAdvancedActions()
                : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_042")
            {
                Assert.That(original.Name, Is.EqualTo("血脉贲张"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Red, ManaColor.Green }));
            }
            CardPlayActions.Play(player, context, card, original, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static MK.Logic.Data.Monster Enemy()
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_032");
            Assert.That(enemy.Attack, Is.EqualTo(4)); Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Armor, Is.EqualTo(3)); Assert.That(enemy.Fame, Is.EqualTo(2));
            Assert.That(enemy.Abilities, Is.Empty); return enemy;
        }

        private static TurnMachine Begin(PlayerState player, ActionContext context, int enemyCount)
        {
            context.Enemies = Enumerable.Range(0, enemyCount).Select(_ => Enemy()).ToList();
            if (enemyCount == 2) Assert.That(context.Enemies[0], Is.Not.SameAs(context.Enemies[1]));
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            return turn;
        }

        private static void Exact(IReadOnlyList<DeedCard> actual, params DeedCard[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++) Assert.That(actual[i], Is.SameAs(expected[i]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActualSelfWoundThenFourDefenseWoundsRespectDrawCapAndDrawnCardsKill(bool strong)
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var draw = OrderedDraw(player);
            var source = Add(player, "advanced_card_042");
            var oldWound = Add(player, "w", CardType.Wound);
            var bolt = Add(player, "advanced_card_041");
            new PlayerTurnEngine().StartTurn(player, DayPart.Day);
            if (strong)
            {
                player.Mana.AddToken(ManaColor.Green);
                context.SelectedActionManaColor = ManaColor.Green;
            }
            Play(player, context, source, strong);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.SelectedActionManaColor, Is.Null);
            Assert.That(context.WoundDrawRemaining, Is.EqualTo(3));
            Assert.That(context.FirstWoundIgnored, Is.EqualTo(strong));
            Exact(player.Deck.DrawPile, draw);
            Exact(player.Deck.Hand, oldWound, bolt);
            Assert.That(player.Deck.RemovedCards, Is.Empty);

            // This wound is caused by a real original card outside battle, not a test-only gain command.
            Play(player, context, bolt);
            Assert.That(context.InBattle, Is.False);
            Assert.That(player.Wounds, Is.EqualTo(strong ? 1 : 2));
            Assert.That(player.Deck.Hand.Count(c => ReferenceEquals(c, oldWound)), Is.EqualTo(1));
            Exact(player.Deck.Hand.Where(c => c.Type != CardType.Wound).ToArray(), draw[0]);
            Exact(player.Deck.DrawPile, draw.Skip(1).ToArray());
            Assert.That(context.WoundDrawRemaining, Is.EqualTo(strong ? 3 : 2));
            Assert.That(context.FirstWoundIgnored, Is.False);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            if (strong)
            {
                Assert.That(player.Deck.RemovedCards.Count, Is.EqualTo(1));
                Assert.That(player.Deck.RemovedCards.Single().Type, Is.EqualTo(CardType.Wound));
                Assert.That(player.Deck.RemovedCards.Single(), Is.Not.SameAs(oldWound));
            }
            else Assert.That(player.Deck.RemovedCards, Is.Empty);

            var turn = Begin(player, context, 2);
            Assert.That(player.Armor, Is.EqualTo(2));
            turn.SetBlock(0, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(damage.TotalWounds, Is.EqualTo(4));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(4));
            Assert.That(context.IsKnockedOut, Is.False, "The pre-battle self wound does not count as a wound in this combat");
            Assert.That(player.Wounds, Is.EqualTo(strong ? 5 : 6));
            Assert.That(context.WoundDrawRemaining, Is.Zero);
            Assert.That(context.FirstWoundIgnored, Is.False);
            int drawn = strong ? 4 : 3;
            Exact(player.Deck.Hand.Where(c => c.Type != CardType.Wound).ToArray(), draw.Take(drawn).ToArray());
            Exact(player.Deck.DrawPile, draw.Skip(drawn).ToArray());
            Assert.That(player.Deck.Hand.Count(c => ReferenceEquals(c, oldWound)), Is.EqualTo(1));

            Play(player, context, draw[0], true); // Original Fury uses the original Bolt's red crystal.
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(context.MeleePool, Is.EqualTo(4));
            Play(player, context, draw[1], false, 0); // Original Determination attack2.
            Assert.That(context.MeleePool, Is.EqualTo(6));
            var hit = turn.Attack(new[] { 0, 1 }, context);
            Assert.That(hit.PrintedPower, Is.EqualTo(6)); Assert.That(hit.EffectivePower, Is.EqualTo(6));
            Assert.That(hit.RequiredPower, Is.EqualTo(6)); Assert.That(hit.Kills, Is.EqualTo(2));
            Assert.That(player.Fame, Is.EqualTo(4));
            turn.FinishBattle(context);
            context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(context.WoundDrawRemaining, Is.Zero); Assert.That(context.FirstWoundIgnored, Is.False);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(4));
            foreach (var card in new[] { source, bolt, draw[0], draw[1] })
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Exact(player.Deck.Hand.Where(c => c.Type != CardType.Wound).ToArray(), draw.Skip(2).Take(drawn - 2).ToArray());
            Exact(player.Deck.DrawPile, draw.Skip(drawn).ToArray());
            Assert.That(player.Deck.RemovedCards.Count, Is.EqualTo(strong ? 1 : 0));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
        }

        [Test]
        public void RedPaymentAndRealPoisonDiscardWoundsDoNotConsumeHandWoundReward()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var draw = OrderedDraw(player); var source = Add(player, "advanced_card_042");
            var oldWound = Add(player, "w", CardType.Wound);
            player.Mana.AddToken(ManaColor.Red); context.SelectedActionManaColor = ManaColor.Red;
            Play(player, context, source, true);
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_001");
            Assert.That(enemy.Attack, Is.EqualTo(6)); Assert.That(enemy.AttackElement, Is.EqualTo(Element.Fire));
            Assert.That(enemy.Abilities, Does.Contain(Ability.Poison));
            context.Enemies = new List<Monster> { enemy };
            var game = new GameState(); game.Players.Add(player); var turn = new TurnMachine(game);
            turn.BeginBattle(context); turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(3));
            Assert.That(player.Deck.DiscardPile.Count(c => c.Type == CardType.Wound), Is.EqualTo(3));
            Assert.That(player.Wounds, Is.EqualTo(3)); // Old wound plus two retained new hand wounds.
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, oldWound)), Is.True);
            Assert.That(player.Deck.RemovedCards.Count, Is.EqualTo(1));
            Assert.That(player.Deck.RemovedCards.Single(), Is.Not.SameAs(oldWound));
            Assert.That(context.WoundDrawRemaining, Is.EqualTo(1));
            Exact(player.Deck.Hand.Where(c => c.Type != CardType.Wound).ToArray(), draw.Take(3).ToArray());
            Exact(player.Deck.DrawPile, draw.Skip(3).ToArray());
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            turn.FinishBattle(context);
        }

        [Test]
        public void UnusedStrongPermissionExpiresBeforeNextTurnActualEnemyWounds()
        {
            var player = new PlayerState(); var context = new ActionContext { DayPart = DayPart.Day };
            var draw = OrderedDraw(player); var source = Add(player, "advanced_card_042");
            var oldWound = Add(player, "w", CardType.Wound);
            player.Mana.AddToken(ManaColor.Green); context.SelectedActionManaColor = ManaColor.Green;
            Play(player, context, source, true);
            Assert.That(context.WoundDrawRemaining, Is.EqualTo(3)); Assert.That(context.FirstWoundIgnored, Is.True);
            context.SkipDraw = true;
            var turns = new PlayerTurnEngine(); turns.EndTurn(player, DayPart.Day, null, context);
            Assert.That(context.WoundDrawRemaining, Is.Zero); Assert.That(context.FirstWoundIgnored, Is.False);
            Exact(player.Deck.DrawPile, draw); Exact(player.Deck.Hand, oldWound);
            turns.StartTurn(player, DayPart.Day);
            var turn = Begin(player, context, 1); turn.SetBlock(0, Element.Physical);
            var damage = turn.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(damage.TotalWounds, Is.EqualTo(2)); Assert.That(player.Wounds, Is.EqualTo(3));
            Exact(player.Deck.DrawPile, draw);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Deck.Hand.Count(c => ReferenceEquals(c, oldWound)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(source));
            turn.FinishBattle(context);
        }
    }
}
