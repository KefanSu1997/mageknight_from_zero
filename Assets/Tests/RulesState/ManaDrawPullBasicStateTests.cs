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
    public sealed class ManaDrawPullBasicStateTests
    {
        private sealed class CountingRandom : Random
        {
            public int Calls { get; private set; }
            public override int Next(int maxValue) { Calls++; return 0; }
        }

        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player, string id)
        {
            var card = new DeedCard(id, CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong = false, int option = 0) => CardPlayActions.Play(player, context, card,
                CardJsonLoader.LoadBasicActions().Single(c => c.Id == card.Id), strong, option);

        private static (ManaSource Source, CountingRandom Random, ActionContext Context) Setup(
            DayPart day, params ManaColor[] faces)
        {
            var random = new CountingRandom();
            var source = new ManaSource(1, () => day, random);
            Assert.That(source.Dice.Count, Is.EqualTo(faces.Length));
            for (int i = 0; i < faces.Length; i++) source.Dice[i].TurnTo(faces[i]);
            var map = new MapState();
            for (int q = 0; q <= 4; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { TerrainType.Plains });
            return (source, random, new ActionContext { ManaSource = source, DayPart = day, MovementMap = map });
        }

        private static void Move(PlayerState player, ActionContext context, int points)
        {
            Assert.That(context.MovementPool, Is.EqualTo(points));
            for (int q = 1; q <= points / 2; q++)
            {
                Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0),
                    context.MovementMap, context), Is.True);
                Assert.That(context.MovementPool, Is.EqualTo(points - 2 * q));
            }
        }

        private static void End(PlayerState player, ActionContext context, ManaSource source,
            ManaDie[] originalDice, params DeedCard[] played)
        {
            Assert.That(player.Deck.PlayedCards.Count, Is.EqualTo(played.Length));
            for (int i = 0; i < played.Length; i++) Assert.That(player.Deck.PlayedCards[i], Is.SameAs(played[i]));
            new PlayerTurnEngine().EndTurn(player, context.DayPart, source, context);
            Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(source.TurnClaims, Is.Empty);
            Assert.That(source.Dice.Count, Is.EqualTo(originalDice.Length));
            foreach (var die in originalDice)
                Assert.That(source.Dice.Count(d => ReferenceEquals(d, die)), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens, Is.Empty);
            Assert.That(context.ExtraManaDice, Is.Zero);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            foreach (var card in played)
                Assert.That(player.Deck.DiscardPile.Count(c => ReferenceEquals(c, card)), Is.EqualTo(1));
        }

        [Test]
        public void ManaDrawBaseUsesOneAdditionalRealDieForSecondOriginalMarch()
        {
            var player = new PlayerState();
            var (source, random, context) = Setup(DayPart.Day, ManaColor.Green, ManaColor.Green, ManaColor.White);
            var dice = source.Dice.ToArray();
            var draw = Add(player, "basic_card_009");
            var firstMarch = Add(player, "basic_card_000");
            var secondMarch = Add(player, "basic_card_000");
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.Green);
            Play(player, context, draw);
            Play(player, context, firstMarch, true);
            Assert.That(context.MovementPool, Is.EqualTo(4));
            CardManaActions.UseSourceDie(player, context, dice[1], ManaColor.Green, extra: true);
            Assert.That(source.Dice.Contains(dice[0]), Is.False);
            Assert.That(source.Dice.Contains(dice[1]), Is.False);
            Assert.That(player.HeldManaDie, Is.SameAs(dice[0]));
            Assert.That(context.ExtraManaDice, Is.Zero);
            Play(player, context, secondMarch, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Move(player, context, 8);
            int rolls = random.Calls;
            End(player, context, source, dice, draw, firstMarch, secondMarch);
            Assert.That(random.Calls - rolls, Is.EqualTo(2), "Ordinary and extra basic-use dice both reroll on return");
        }

        [Test]
        public void ManaDrawStrongTransformsOneRealDieToTwoManaAndReturnsItWithoutReroll()
        {
            var player = new PlayerState();
            var (source, random, context) = Setup(DayPart.Day, ManaColor.Blue, ManaColor.Red, ManaColor.White);
            var dice = source.Dice.ToArray();
            var draw = Add(player, "basic_card_009");
            var firstMarch = Add(player, "basic_card_000");
            var secondMarch = Add(player, "basic_card_000");
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.Blue);
            context.ManaDiceToTransform.Add(dice[1]);
            Play(player, context, draw, true, (int)ManaColor.Green);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(2));
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Green));
            Assert.That(source.Dice.Single(), Is.SameAs(dice[2]));
            Play(player, context, firstMarch, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Play(player, context, secondMarch, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Move(player, context, 8);
            int rolls = random.Calls;
            End(player, context, source, dice, draw, firstMarch, secondMarch);
            Assert.That(random.Calls - rolls, Is.EqualTo(1), "Only the ordinary fee die rerolls");
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Green));
            Assert.That(dice[2].Face, Is.EqualTo(ManaColor.White));
        }

        [Test]
        public void ManaPullBaseBlackWildnessAppliesOnlyToItsExtraDie()
        {
            var player = new PlayerState();
            var (source, random, context) = Setup(DayPart.Day, ManaColor.Green, ManaColor.Black, ManaColor.White);
            var dice = source.Dice.ToArray();
            var pull = Add(player, "basic_card_013");
            var firstMarch = Add(player, "basic_card_000");
            var secondMarch = Add(player, "basic_card_000");
            Play(player, context, pull);
            Assert.Throws<InvalidOperationException>(() =>
                CardManaActions.UseSourceDie(player, context, dice[1], ManaColor.Green));
            Assert.That(source.Dice, Is.EqualTo(dice));
            Assert.That(player.HeldManaDie, Is.Null);
            Assert.That(context.ExtraManaDice, Is.EqualTo(1));
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.Green);
            Play(player, context, firstMarch, true);
            CardManaActions.UseSourceDie(player, context, dice[1], ManaColor.Green, extra: true);
            Assert.That(player.HeldManaDie, Is.SameAs(dice[0]));
            Assert.That(context.ExtraManaDice, Is.Zero);
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Black), "Basic use generates a chosen mana; it does not turn the die");
            Play(player, context, secondMarch, true);
            Move(player, context, 8);
            int rolls = random.Calls;
            End(player, context, source, dice, pull, firstMarch, secondMarch);
            Assert.That(random.Calls - rolls, Is.EqualTo(2));
        }

        [Test]
        public void ManaPullStrongTransformsTwoRealDiceAndBothColorsPayOriginalCards()
        {
            var player = new PlayerState();
            var (source, random, context) = Setup(DayPart.Day, ManaColor.White, ManaColor.Blue, ManaColor.White);
            var dice = source.Dice.ToArray();
            var pull = Add(player, "basic_card_013");
            var march = Add(player, "basic_card_000");
            var fury = Add(player, "basic_card_021");
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.White);
            context.ManaDiceToTransform.Add(dice[1]);
            context.ManaDiceToTransform.Add(dice[2]);
            Play(player, context, pull, true, (int)ManaColor.Green | ((int)ManaColor.Red << 8));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.White), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.EqualTo(1));
            Assert.That(source.Dice, Is.Empty);
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Green));
            Assert.That(dice[2].Face, Is.EqualTo(ManaColor.Red));
            Play(player, context, march, true);
            Move(player, context, 4);
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == "monster_013");
            context.Enemies = new List<MK.Logic.Data.Monster> { enemy };
            var game = new GameState(); game.Players.Add(player);
            var machine = new TurnMachine(game);
            machine.BeginBattle(context);
            machine.ApplyRanged(context.Enemies, Array.Empty<AttackAllocation>(), context);
            machine.SetBlock(0, Element.Physical);
            var defense = machine.ResolveDefense(context.Enemies, Array.Empty<BlockAllocation>(), context);
            Assert.That(defense.TotalWounds, Is.EqualTo(2));
            Play(player, context, fury, true);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
            var attack = machine.Attack(new[] { 0 }, context);
            Assert.That(attack.EffectivePower, Is.EqualTo(4));
            Assert.That(attack.RequiredPower, Is.EqualTo(4));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(2));
            machine.FinishBattle(context);
            int rolls = random.Calls;
            End(player, context, source, dice, pull, march, fury);
            Assert.That(random.Calls - rolls, Is.EqualTo(1), "Only the ordinary white fee die rerolls");
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Green));
            Assert.That(dice[2].Face, Is.EqualTo(ManaColor.Red));
        }

        [Test]
        public void ForbiddenGoldTransformationRejectsBeforePrintedBluePayment()
        {
            var player = new PlayerState();
            var (source, _, context) = Setup(DayPart.Day, ManaColor.Blue, ManaColor.Red, ManaColor.White);
            var dice = source.Dice.ToArray();
            var draw = Add(player, "basic_card_009");
            CardManaActions.UseSourceDie(player, context, dice[0], ManaColor.Blue);
            context.ManaDiceToTransform.Add(dice[1]);
            int claims = source.TurnClaims.Count;
            Assert.Throws<InvalidOperationException>(() => Play(player, context, draw, true, (int)ManaColor.Gold));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
            Assert.That(source.TurnClaims.Count, Is.EqualTo(claims));
            Assert.That(source.Dice, Is.EqualTo(dice.Skip(1)));
            Assert.That(dice[1].Face, Is.EqualTo(ManaColor.Red));
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(draw));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
        }
    }
}
