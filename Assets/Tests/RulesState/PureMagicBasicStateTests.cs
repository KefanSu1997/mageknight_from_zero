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
    public sealed class PureMagicBasicStateTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private static DeedCard Add(PlayerState player)
        {
            var card = new DeedCard("advanced_card_013", CardType.Action);
            player.Deck.Hand.Add(card);
            return card;
        }

        private static void Play(PlayerState player, ActionContext context, DeedCard card,
            bool strong, int option)
        {
            var original = CardJsonLoader.LoadAdvancedActions().Single(c => c.Id == card.Id);
            Assert.That(original.Name, Is.EqualTo("纯净魔法"));
            Assert.That(original.RequiredCrystals, Is.EqualTo(new[] { ManaColor.Blue }));
            CardPlayActions.Play(player, context, card, original, strong, option);
        }

        private static void Fund(PlayerState player, ManaColor chosen, bool strong)
        {
            player.Mana.AddToken(chosen);
            if (strong) player.Mana.AddToken(ManaColor.Blue);
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(strong ? 2 : 1));
        }

        private static void PaidOutput(PlayerState player, ActionContext context, DeedCard card,
            ManaColor chosen, bool strong)
        {
            int value = strong ? 7 : 4;
            Assert.That(player.Mana.Tokens, Is.Empty);
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(context.MovementPool, Is.EqualTo(chosen == ManaColor.Green ? value : 0));
            Assert.That(context.InfluencePool, Is.EqualTo(chosen == ManaColor.White ? value : 0));
            Assert.That(context.BlockPool, Is.EqualTo(chosen == ManaColor.Blue ? value : 0));
            Assert.That(context.MeleePool, Is.EqualTo(chosen == ManaColor.Red ? value : 0));
            Assert.That(player.Deck.PlayedCards.Single(), Is.SameAs(card));
            Assert.That(player.Deck.Hand, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(player.Wounds, Is.Zero);
        }

        private static void End(PlayerState player, ActionContext context, DeedCard card)
        {
            if (context.InBattle) UnitDamageResolver.EndBattle(player, context);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, null, context);
            Assert.That(player.Deck.DiscardPile.Single(), Is.SameAs(card));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.Hand, Is.Empty);
        }

        private static ActionContext Battle(bool strong, Phase phase)
        {
            var enemy = MonsterJsonLoader.LoadMonsters(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"))
                .Single(m => m.Id == (strong ? "monster_027" : "monster_013"));
            // Both printed tokens were inspected; 027 is physical6, armor5, fame4, no abilities.
            Assert.That(enemy.Attack, Is.EqualTo(strong ? 6 : 4));
            Assert.That(enemy.AttackElement, Is.EqualTo(Element.Physical));
            Assert.That(enemy.Armor, Is.EqualTo(strong ? 5 : 4));
            Assert.That(enemy.Fame, Is.EqualTo(strong ? 4 : 2));
            Assert.That(enemy.Abilities, Is.Empty);
            return new ActionContext { DayPart = DayPart.Day, InBattle = true,
                CombatPhase = phase, Enemies = new List<MK.Logic.Data.Monster> { enemy } };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GreenPaysSelectedAndPrintedManaThenMovesAcrossRealTerrain(bool strong)
        {
            var player = new PlayerState();
            var card = Add(player);
            var map = new MapState();
            var terrain = strong
                ? new[] { TerrainType.Plains, TerrainType.Forest, TerrainType.Plains, TerrainType.Plains }
                : new[] { TerrainType.Plains, TerrainType.Plains, TerrainType.Plains };
            for (int q = 0; q < terrain.Length; q++)
                map.Placed[new AxialCoord(q, 0)] = new MapTile(TileSet.Countryside, q, new[] { terrain[q] });
            var context = new ActionContext { DayPart = DayPart.Day, MovementMap = map };
            Fund(player, ManaColor.Green, strong);
            Play(player, context, card, strong, (int)ManaColor.Green);
            PaidOutput(player, context, card, ManaColor.Green, strong);
            int remaining = strong ? 7 : 4;
            for (int q = 1; q < terrain.Length; q++)
            {
                int cost = strong && q == 1 ? 3 : 2;
                Assert.That(MovementService.GetCost(new AxialCoord(q, 0), map, DayPart.Day, context), Is.EqualTo(cost));
                Assert.That(new MovementService().TryMoveUsingPool(player, new AxialCoord(q, 0), map, context), Is.True);
                remaining -= cost;
                Assert.That(context.MovementPool, Is.EqualTo(remaining));
                Assert.That(player.Position, Is.EqualTo(new AxialCoord(q, 0)));
            }
            Assert.That(context.MovementPool, Is.Zero);
            End(player, context, card);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WhiteInfluenceRecruitsActualOriginalPeasantsFromOffer(bool strong)
        {
            var player = new PlayerState();
            var context = new ActionContext();
            var card = Add(player);
            var candidate = CardJsonLoader.LoadUnit("army_2_006");
            var offer = new List<UnitCard> { candidate };
            Assert.That(candidate.InfluenceCost, Is.EqualTo(4));
            Assert.That(candidate.RecruitLocationMask, Is.EqualTo(RecruitLocation.Village));
            Fund(player, ManaColor.White, strong);
            Play(player, context, card, strong, (int)ManaColor.White);
            PaidOutput(player, context, card, ManaColor.White, strong);
            RecruitmentService.BeginInteraction(player, context, RecruitLocation.Village);
            Assert.That(RecruitmentService.GetCost(player, candidate, RecruitLocation.Village, context), Is.EqualTo(4));
            Assert.That(CardUnitActions.TryRecruit(player, context, offer, 0, RecruitLocation.Village), Is.True);
            Assert.That(context.InfluencePool, Is.EqualTo(strong ? 3 : 0));
            Assert.That(offer, Is.Empty);
            Assert.That(player.Units.Single().Card, Is.SameAs(candidate));
            Assert.That(player.Units.Single().IsReady, Is.True);
            Assert.That(player.Units.Single().Wounds, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            Assert.That(player.Reputation, Is.Zero);
            End(player, context, card);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BlueBlockActuallyStopsPrintedOriginalEnemyAttack(bool strong)
        {
            var player = new PlayerState();
            var context = Battle(strong, Phase.Block);
            var enemy = context.Enemies.Single();
            var card = Add(player);
            Fund(player, ManaColor.Blue, strong);
            Assert.That(player.Mana.Tokens[ManaColor.Blue], Is.EqualTo(strong ? 2 : 1));
            Play(player, context, card, strong, (int)ManaColor.Blue);
            PaidOutput(player, context, card, ManaColor.Blue, strong);
            var result = CardCombatActions.Block(player, context, context.Enemies, 0);
            Assert.That(result.PrintedPower, Is.EqualTo(strong ? 7 : 4));
            Assert.That(result.EffectivePower, Is.EqualTo(strong ? 7 : 4));
            Assert.That(result.RequiredPower, Is.EqualTo(strong ? 6 : 4));
            Assert.That(result.Wounds, Is.Zero);
            Assert.That(result.Kills, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.WasBlockedThisBattle(enemy), Is.True);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(player.Fame, Is.Zero);
            End(player, context, card);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RedMeleeConsumesPrintedPowerAndDefeatsActualOriginalEnemy(bool strong)
        {
            var player = new PlayerState();
            var context = Battle(strong, Phase.Melee);
            var card = Add(player);
            Fund(player, ManaColor.Red, strong);
            Play(player, context, card, strong, (int)ManaColor.Red);
            PaidOutput(player, context, card, ManaColor.Red, strong);
            var result = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(result.PrintedPower, Is.EqualTo(strong ? 7 : 4));
            Assert.That(result.EffectivePower, Is.EqualTo(strong ? 7 : 4));
            Assert.That(result.RequiredPower, Is.EqualTo(strong ? 5 : 4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(strong ? 4 : 2));
            Assert.That(context.DefeatedEnemyIndices.SetEquals(new[] { 0 }), Is.True);
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
            End(player, context, card);
        }

        private static void RejectedUnchanged(PlayerState player, ActionContext context, DeedCard card, int option)
        {
            Assert.Throws<InvalidOperationException>(() => Play(player, context, card, true, option));
            Assert.That(player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals, Is.Empty);
            Assert.That(player.Deck.Hand.Single(), Is.SameAs(card));
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile, Is.Empty);
            Assert.That(context.MovementPool, Is.Zero);
            Assert.That(context.InfluencePool, Is.Zero);
            Assert.That(context.BlockPool, Is.Zero);
            Assert.That(context.MeleePool, Is.Zero);
        }

        [Test]
        public void StrongBlueWithOnlyOneBlueRejectsEntirePaymentAndPreservesCard()
        {
            var player = new PlayerState();
            var card = Add(player);
            player.Mana.AddToken(ManaColor.Blue);
            RejectedUnchanged(player, Battle(true, Phase.Block), card, (int)ManaColor.Blue);
        }

        [Test]
        public void InvalidGoldOptionRejectsBeforeAnyPrintedManaPayment()
        {
            var player = new PlayerState();
            var card = Add(player);
            player.Mana.AddToken(ManaColor.Blue);
            RejectedUnchanged(player, new ActionContext(), card, (int)ManaColor.Gold);
        }
    }
}
