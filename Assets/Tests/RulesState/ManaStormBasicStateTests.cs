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
    public sealed class ManaStormBasicStateTests
    {
        private sealed class SequenceRandom : Random
        {
            private readonly Queue<int> values;
            public readonly List<int> Sides = new();
            public int Remaining => values.Count;
            public SequenceRandom(params int[] rolls) => values = new Queue<int>(rolls);
            public override int Next(int maxValue)
            {
                Sides.Add(maxValue);
                if (values.Count == 0) throw new InvalidOperationException("Unexpected additional roll");
                int value = values.Dequeue();
                if (value < 0 || value >= maxValue) throw new InvalidOperationException("Wrong roll range");
                return value;
            }
        }

        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action); player.Deck.Hand.Add(card); return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0) => CardPlayActions.Play(player, context, card,
                (card.Id.StartsWith("advanced_") ? CardJsonLoader.LoadAdvancedActions()
                    : CardJsonLoader.LoadBasicActions()).Single(c => c.Id == card.Id), strong, option);

        private static ActionContext Context(ManaSource source, DayPart day)
        {
            var map = new MapState();
            for (int q = 0; q <= 6; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return new ActionContext { ManaSource = source, MovementMap = map, DayPart = day };
        }

        private static void Walk(PlayerState player, ActionContext context, int points)
        {
            Assert.That(context.MovementPool, Is.EqualTo(points));
            for (int q = 1; q <= points / 2; q++)
            {
                Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0),
                    context.MovementMap, context), Is.True);
                Assert.That(context.MovementPool, Is.EqualTo(points - 2 * q));
            }
        }

        private static void FuryAfterRealDefense(PlayerState player, ActionContext context, DeedCard fury)
        {
            var game = new GameState(); game.Players.Add(player);
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_013");
            Assert.That(enemy.Attack, Is.EqualTo(4)); Assert.That(enemy.Armor, Is.EqualTo(4));
            var enemies = new List<Monster> { enemy }; context.Enemies = enemies;
            var turn = new TurnMachine(game); turn.BeginBattle(context);
            turn.ApplyRanged(enemies, Array.Empty<AttackAllocation>(), context);
            turn.SetBlock(0, Element.Physical);
            turn.ResolveDefense(enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(player.Wounds, Is.EqualTo(2));
            Assert.That(context.BattleWoundsReceived, Is.EqualTo(2));
            Assert.That(context.CombatPhase, Is.EqualTo(Phase.Melee));
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            var result = turn.Attack(new[] { 0 }, context);
            Assert.That(result.PrintedPower, Is.EqualTo(4));
            Assert.That(result.EffectivePower, Is.EqualTo(4));
            Assert.That(result.RequiredPower, Is.EqualTo(4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            turn.FinishBattle(context);
        }

        private static void End(PlayerState player, ActionContext context, ManaDie[] dice,
            params DeedCard[] cards)
        {
            var source = context.ManaSource;
            new PlayerTurnEngine().EndTurn(player, context.DayPart, source, context);
            Assert.That(source.Dice, Is.EquivalentTo(dice));
            foreach (var die in dice)
                Assert.That(source.Dice.Count(d => ReferenceEquals(d, die)), Is.EqualTo(1));
            Assert.That(source.TurnClaims, Is.Empty); Assert.That(source.RoundClaims, Is.Empty);
            Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(context.ExtraManaDice, Is.Zero);
            Assert.That(context.BlackManaWild, Is.False); Assert.That(context.GoldManaWild, Is.False);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(cards.Length));
            foreach (var card in cards)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.Hand.Count, Is.EqualTo(2));
            Assert.That(player.Deck.Hand.All(c => c.Type == CardType.Wound), Is.True);
        }

        [Test]
        public void BaseCrystalPaysMarchAndOnlySelectedDieRerollsWithoutUsingOrdinaryAllowance()
        {
            // Three initial five-face source rolls; card's one six-face roll; ordinary return roll.
            var random = new SequenceRandom(2, 1, 3, 0, 1);
            var player = new PlayerState(); var source = new ManaSource(1, () => DayPart.Day, random);
            var context = Context(source, DayPart.Day); var dice = source.Dice.ToArray();
            Assert.That(dice.Select(d => d.Face), Is.EqualTo(new[] { ManaColor.Green, ManaColor.Blue, ManaColor.White }));
            var storm = Add(player, "advanced_card_022"); var march = Add(player, "basic_card_000");
            var fury = Add(player, "basic_card_021");
            Play(player, context, storm, option: 0);
            Assert.That(random.Sides, Is.EqualTo(new[] { 6, 6, 6, 6 }));
            Assert.That(source.Dice, Is.EqualTo(dice));
            Assert.That(dice.Select(d => d.Face), Is.EqualTo(new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White }));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(source.TurnClaims, Is.Empty); Assert.That(player.HeldManaDie, Is.Null);
            Play(player, context, march, true);
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(context.SpentCrystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Walk(player, context, 4);
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.Red);
            Assert.That(player.HeldManaDie, Is.SameAs(dice[0]));
            Assert.That(source.TurnClaims.Single().Key, Is.SameAs(dice[0]));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            FuryAfterRealDefense(player, context, fury);
            End(player, context, dice, storm, march, fury);
            Assert.That(dice[0].Face, Is.EqualTo(ManaColor.Blue));
            Assert.That(random.Sides, Is.EqualTo(new[] { 6, 6, 6, 6, 6 }));
            Assert.That(random.Remaining, Is.Zero);
        }

        [TestCase(DayPart.Day)]
        [TestCase(DayPart.Night)]
        public void StrongRerollsAllSourceDiceAndSpendsOrdinaryPlusThreeExtraWithBothWildFaces(DayPart day)
        {
            var random = new SequenceRandom(0, 1, 2, 3, 2, 5, 4, 0, 1, 1, 1, 1);
            var player = new PlayerState(); var source = new ManaSource(2, () => day, random);
            var context = Context(source, day); var dice = source.Dice.ToArray();
            player.Mana.AddToken(ManaColor.White);
            var storm = Add(player, "advanced_card_022");
            var marches = Enumerable.Range(0, 3).Select(_ => Add(player, "basic_card_000")).ToArray();
            var fury = Add(player, "basic_card_021");
            Play(player, context, storm, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(random.Sides, Is.EqualTo(new[] { 6, 6, 6, 6, 6, 6, 6, 6 }));
            Assert.That(source.Dice, Is.EqualTo(dice));
            Assert.That(dice.Select(d => d.Face), Is.EqualTo(new[] { ManaColor.Green, ManaColor.Black, ManaColor.Gold, ManaColor.Red }));
            Assert.That(context.ExtraManaDice, Is.EqualTo(3));
            Assert.That(context.BlackManaWild, Is.True); Assert.That(context.GoldManaWild, Is.True);
            // Night's ordinary Gold proves the permission is not restricted to extra dice.
            var order = day == DayPart.Day ? new[] { 0, 1, 2, 3 } : new[] { 2, 0, 1, 3 };
            for (int i = 0; i < order.Length; i++)
            {
                var chosen = i == 3 ? ManaColor.Red : ManaColor.Green;
                CardManaActions.UseSourceDie(player, context, dice[order[i]], chosen, extra: i > 0);
                Assert.That(source.TurnClaims.ContainsKey(dice[order[i]]), Is.True);
                Assert.That(source.TurnClaims[dice[order[i]]], Is.SameAs(player));
                Assert.That(source.TurnClaims.Count, Is.EqualTo(i + 1));
                Assert.That(source.Dice.Count, Is.EqualTo(3 - i));
                Assert.That(context.ExtraManaDice, Is.EqualTo(3 - i));
                Assert.That(player.HeldManaDie, Is.SameAs(dice[order[0]]));
            }
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(3));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            for (int i = 0; i < marches.Length; i++)
            {
                Play(player, context, marches[i], true);
                Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(2 - i));
                Assert.That(context.MovementPool, Is.EqualTo(4 * (i + 1)));
            }
            Walk(player, context, 12);
            FuryAfterRealDefense(player, context, fury);
            End(player, context, dice, new[] { storm }.Concat(marches).Concat(new[] { fury }).ToArray());
            Assert.That(dice.All(d => d.Face == ManaColor.Blue), Is.True);
            Assert.That(random.Sides.Skip(8), Is.EqualTo(new[] { 6, 6, 6, 6 }));
            Assert.That(random.Remaining, Is.Zero);
        }

        [Test]
        public void BaseGoldChoiceRejectsBeforeRerollCrystalOrSourceCardMovement()
        {
            var random = new SequenceRandom(4, 1, 2);
            var source = new ManaSource(1, () => DayPart.Day, random);
            var player = new PlayerState(); var context = Context(source, DayPart.Day);
            var dice = source.Dice.ToArray(); var storm = Add(player, "advanced_card_022");
            Assert.That(dice[0].Face, Is.EqualTo(ManaColor.Gold));
            Assert.Throws<InvalidOperationException>(() => Play(player, context, storm, option: 0));
            Assert.That(random.Sides.Count, Is.EqualTo(3));
            Assert.That(source.Dice, Is.EqualTo(dice));
            Assert.That(dice[0].Face, Is.EqualTo(ManaColor.Gold));
            Assert.That(source.TurnClaims, Is.Empty); Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.Zero);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(storm));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }
    }
}
