using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    public sealed class AmbushBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0)
        {
            var original = (card.Id.StartsWith("basic_card_")
                ? CardJsonLoader.LoadBasicActions() : CardJsonLoader.LoadAdvancedActions())
                .Single(c => c.Id == card.Id);
            if (card.Id == "advanced_card_023")
            {
                Assert.That(original.Name, Is.EqualTo("伏击"));
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Green }));
            }
            else if (strong)
                Assert.That(original.RequiredCrystals, Is.EqualTo(new[]
                    { card.Id == "basic_card_007" ? ManaColor.White : ManaColor.Red }));
            CardPlayActions.Play(player, context, card, original, strong, option);
            Assert.That(player.Deck.PlayedCards.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Any(c => ReferenceEquals(c, card)), Is.False);
        }

        private static ActionContext Route()
        {
            var context = new ActionContext { DayPart = DayPart.Day, CurrentTerrain = TerrainType.Plains,
                MovementMap = new MapState() };
            for (int q = 0; q <= 2; q++)
                context.MovementMap.Placed[new AxialCoord(q, 0)] =
                    new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return context;
        }

        private static void AmbushAndMove(PlayerState player, ActionContext context, DeedCard ambush, bool strong)
        {
            if (strong) player.Mana.AddToken(ManaColor.Green);
            Play(player, context, ambush, strong);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.MovementPool, Is.EqualTo(strong ? 4 : 2));
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(context.RangedPool, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.AmbushTriggered, Is.False);
            int steps = strong ? 2 : 1;
            for (int q = 1; q <= steps; q++)
            {
                var destination = new AxialCoord(q, 0);
                Assert.That(MovementService.GetCost(destination, context.MovementMap, DayPart.Day, context), Is.EqualTo(2));
                Assert.That(new MovementService().TryMoveUsingPool(player, destination, context.MovementMap, context), Is.True);
                Assert.That(player.Position, Is.EqualTo(destination));
                Assert.That(context.MovementPool, Is.EqualTo((steps - q) * 2));
            }
        }

        private static MK.Logic.Data.Monster Enemy()
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_013");
            Assert.That(enemy.Attack, Is.EqualTo(4));
            Assert.That(enemy.Armor, Is.EqualTo(4));
            Assert.That(enemy.Fame, Is.EqualTo(2));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Abilities, Is.Empty);
            return enemy;
        }

        private static TurnMachine Begin(PlayerState player, ActionContext context, params MK.Logic.Data.Monster[] enemies)
        {
            context.Enemies = new List<MK.Logic.Data.Monster>(enemies);
            var game = new GameState(); game.Players.Add(player);
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Ranged));
            return turn;
        }

        private static void Defense(TurnMachine turn, PlayerState player, ActionContext context,
            int printed, int wounds)
        {
            var parts = context.CombatPower.AvailableBlocks(0).Select(b => b with { TargetIndex = 0 }).ToArray();
            Assert.That(parts.Sum(b => b.Value), Is.EqualTo(printed));
            Assert.That(player.Armor, Is.EqualTo(2));
            turn.SetBlock(printed, Element.Physical);
            var result = turn.ResolveDefense(context.Enemies, parts, context);
            Assert.That(result.TotalWounds, Is.EqualTo(wounds));
            Assert.That(player.Wounds, Is.EqualTo(wounds));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(wounds));
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
        }

        private static void KillAndEnd(TurnMachine turn, PlayerState player, ActionContext context,
            int fame, int wounds, params DeedCard[] originals)
        {
            Assert.That(context.MeleePool, Is.EqualTo(4));
            var hit = turn.Attack(new[] { 0 }, context);
            Assert.That(hit.PrintedPower, Is.EqualTo(4));
            Assert.That(hit.EffectivePower, Is.EqualTo(4));
            Assert.That(hit.RequiredPower, Is.EqualTo(4));
            Assert.That(hit.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(fame));
            Assert.That(context.MeleePool, Is.Zero);
            turn.FinishBattle(context);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DrawPile, Is.Empty);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(originals.Length));
            foreach (var original in originals)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, original)), Is.EqualTo(1));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(wounds));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
            Assert.That(player.Mana.Tokens, Is.Empty);
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(context.InBattle, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void FirstOriginalRangedAttackConsumesAmbushAndLaterBlockAndMeleeStayPrinted(bool strong)
        {
            var player = new PlayerState(); var context = Route();
            var ambush = Add(player, "advanced_card_023");
            var swiftness = Add(player, "basic_card_007");
            var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.White); player.Mana.AddToken(ManaColor.Red);
            AmbushAndMove(player, context, ambush, strong);
            var first = Enemy(); var survivor = Enemy();
            Assert.That(first, Is.Not.SameAs(survivor));
            var turn = Begin(player, context, first, survivor);

            Play(player, context, swiftness, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            int ranged = strong ? 5 : 4;
            Assert.That(context.RangedPool, Is.EqualTo(ranged));
            Assert.That(context.AmbushTriggered, Is.True);
            var parts = context.CombatPower.AvailableAttacks(Phase.Ranged);
            Assert.That(parts.Single().Type, Is.EqualTo(AttackType.Ranged));
            Assert.That(parts.Single().Value, Is.EqualTo(ranged));
            turn.ApplyRanged(context.Enemies,
                new[] { new AttackAllocation(new[] { 0 }, ranged, Element.Physical, Components: parts) }, context);
            Assert.That(context.Enemies.Single(), Is.SameAs(survivor));
            Assert.That(player.Fame, Is.EqualTo(2));
            Assert.That(context.RangedPool, Is.Zero);

            Play(player, context, determination, option: 1);
            Assert.That(context.BlockPool, Is.EqualTo(2), "The first attack already consumed the mutually exclusive bonus");
            Defense(turn, player, context, 2, 2);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            KillAndEnd(turn, player, context, 4, 2, ambush, swiftness, determination, fury);
        }

        [TestCase(false)] [TestCase(true)]
        public void FirstOriginalBlockConsumesAmbushAndLaterOriginalFuryStaysPrinted(bool strong)
        {
            var player = new PlayerState(); var context = Route();
            var ambush = Add(player, "advanced_card_023");
            var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021");
            player.Mana.AddToken(ManaColor.Red);
            AmbushAndMove(player, context, ambush, strong);
            var enemy = Enemy(); var turn = Begin(player, context, enemy);
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);

            Play(player, context, determination, option: 1);
            Assert.That(context.BlockPool, Is.EqualTo(strong ? 6 : 4));
            Assert.That(context.AmbushTriggered, Is.True);
            Defense(turn, player, context, strong ? 6 : 4, 0);
            Assert.That(context.WasBlockedThisBattle(enemy), Is.True);
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            KillAndEnd(turn, player, context, 2, 0, ambush, determination, fury);
        }

        [Test]
        public void UnusedAmbushExpiresBeforeNextTurnOriginalBlockAndActualDamage()
        {
            var player = new PlayerState(); var context = Route();
            var ambush = Add(player, "advanced_card_023");
            var determination = Add(player, "basic_card_016");
            var fury = Add(player, "basic_card_021");
            AmbushAndMove(player, context, ambush, false);
            var turns = new PlayerTurnEngine();
            turns.EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(ambush));
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            turns.StartTurn(player, DayPart.Day);
            player.Mana.AddToken(ManaColor.Red);
            var turn = Begin(player, context, Enemy());
            turn.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            Play(player, context, determination, option: 1);
            Assert.That(context.BlockPool, Is.EqualTo(2), "Previous-turn unused Ambush cannot grant Block +2");
            Defense(turn, player, context, 2, 2);
            Play(player, context, fury, true);
            KillAndEnd(turn, player, context, 2, 2, ambush, determination, fury);
        }
    }
}
