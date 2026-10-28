using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using NUnit.Framework;

namespace MK.Tests.Cards
{
    /// <summary>原灵魂收割者正常出牌/焚毁，直接击杀、单一抗性色奖励及真实卡区。</summary>
    public sealed class SoulHarvesterBasicStateTests
    {
        [SetUp]
        public void OriginalData() => CardJsonLoader.SetBaseDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        [TestCase(false, "monster_027", ManaColor.White, 1, false, 5, 5, 4)]
        [TestCase(true, "monster_008", ManaColor.Green, 2, true, 16, 8, 8)]
        [TestCase(true, "monster_030", ManaColor.Red, 0, false, 8, 8, 5)]
        [TestCase(true, "monster_028", ManaColor.Blue, 0, false, 8, 8, 5)]
        public void OriginalArtifactKillsOriginalEnemyAndGrantsOneCrystal(
            bool burn, string enemyId, ManaColor reward, int furyCount, bool strongFury,
            int printedAttack, int effectiveAttack, int expectedFame)
        {
            var player = new PlayerState();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee, SkipDraw = true };
            context.Enemies = new List<Monster> { CardJsonLoader.LoadMonsters().Single(m => m.Id == enemyId) };
            var artifact = new DeedCard("items_019", CardType.Artifact);
            player.Deck.Hand.Add(artifact);
            var furies = Enumerable.Range(0, furyCount)
                .Select(_ => new DeedCard("basic_card_021", CardType.Action)).ToArray();
            player.Deck.Hand.AddRange(furies);
            if (strongFury) player.Mana.AddToken(ManaColor.Red, furyCount);

            CardPlayActions.PlayArtifact(player, context, artifact,
                CardJsonLoader.LoadItems().Single(c => c.Id == artifact.Id), burn);
            Assert.That(context.MeleePool, Is.EqualTo(burn ? 8 : 3));
            Assert.That(burn ? player.Deck.RemovedCards.Single() : player.Deck.PlayedCards.Single(),
                Is.SameAs(artifact));
            foreach (var fury in furies)
                CardPlayActions.Play(player, context, fury,
                    CardJsonLoader.LoadBasicActions().Single(c => c.Id == fury.Id), strongFury);

            var attack = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(printedAttack));
            Assert.That(attack.EffectivePower, Is.EqualTo(effectiveAttack));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(expectedFame));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(reward), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Mana.Tokens.Values.Sum(), Is.Zero);
            Assert.That(context.MeleePool, Is.Zero);
            Assert.That(player.Deck.Hand, Is.Empty);

            UnitDamageResolver.EndBattle(player, context);
            new PlayerTurnEngine().EndTurn(player, DayPart.Day, new ManaSource(1, () => DayPart.Day), context);
            Assert.That(player.Deck.PlayedCards, Is.Empty);
            Assert.That(player.Deck.DiscardPile.Count, Is.EqualTo(furyCount + (burn ? 0 : 1)));
            Assert.That(player.Deck.DiscardPile.Any(c => ReferenceEquals(c, artifact)), Is.EqualTo(!burn));
            Assert.That(player.Deck.RemovedCards.Any(c => ReferenceEquals(c, artifact)), Is.EqualTo(burn));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(reward), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LaterAttackOnlyReceivesBurnRewardAndNewPhaseExpiresIt(bool burn)
        {
            var player = new PlayerState();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee };
            context.Enemies = new List<Monster>
            {
                CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_027"),
                CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_027")
            };
            var artifact = new DeedCard("items_019", CardType.Artifact);
            player.Deck.Hand.Add(artifact);
            CardPlayActions.PlayArtifact(player, context, artifact,
                CardJsonLoader.LoadItems().Single(c => c.Id == artifact.Id), burn);
            if (!burn) PlayFury(player, context, false);
            Assert.That(CardCombatActions.Attack(player, context, context.Enemies,
                new[] { 0 }, Phase.Melee).Kills, Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));

            PlayFury(player, context, true);
            PlayFury(player, context, false);
            Assert.That(CardCombatActions.Attack(player, context, context.Enemies,
                new[] { 1 }, Phase.Melee).Kills, Is.EqualTo(1));
            int expectedCrystals = burn ? 2 : 1;
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(expectedCrystals));

            CardCombatActions.EnterPhase(context, Phase.End);
            Assert.That(context.CrystalsPerKill, Is.Zero);
            UnitDamageResolver.EndBattle(player, context);
            var game = new GameState();
            game.Players.Add(player);
            new TurnMachine(game).BeginBattle(context);
            CardCombatActions.EnterPhase(context, Phase.Melee);
            context.Enemies = new List<Monster>
                { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_027") };
            PlayFury(player, context, true);
            PlayFury(player, context, false);
            Assert.That(CardCombatActions.Attack(player, context, context.Enemies,
                new[] { 0 }, Phase.Melee).Kills, Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(expectedCrystals));
        }

        [Test]
        public void FireResistantOriginalEnemyCanExplicitlyRewardWhiteInsteadOfRed()
        {
            var player = new PlayerState();
            var context = new ActionContext { InBattle = true, CombatPhase = Phase.Melee };
            context.Enemies = new List<Monster>
                { CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_030") };
            Assert.That(context.Enemies.Single().Abilities, Does.Contain(Ability.FireResist));
            var artifact = new DeedCard("items_019", CardType.Artifact);
            player.Deck.Hand.Add(artifact);
            CardPlayActions.PlayArtifact(player, context, artifact,
                CardJsonLoader.LoadItems().Single(c => c.Id == artifact.Id), option: 1);
            PlayFury(player, context, false);

            var attack = CardCombatActions.Attack(player, context, context.Enemies, new[] { 0 }, Phase.Melee);
            Assert.That(attack.PrintedPower, Is.EqualTo(5));
            Assert.That(attack.EffectivePower, Is.EqualTo(5));
            Assert.That(attack.Kills, Is.EqualTo(1));
            Assert.That(player.Fame, Is.EqualTo(5));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(1));
            Assert.That(player.Mana.Crystals.GetValueOrDefault(ManaColor.Red), Is.Zero);
            Assert.That(player.Mana.Crystals.Values.Sum(), Is.EqualTo(1));
            Assert.That(player.Deck.PlayedCards.Any(c => ReferenceEquals(c, artifact)), Is.True);
            Assert.That(player.Deck.RemovedCards, Is.Empty);
        }

        private static void PlayFury(PlayerState player, ActionContext context, bool strong)
        {
            var fury = new DeedCard("basic_card_021", CardType.Action);
            player.Deck.Hand.Add(fury);
            if (strong) player.Mana.AddToken(ManaColor.Red, 1);
            CardPlayActions.Play(player, context, fury,
                CardJsonLoader.LoadBasicActions().Single(c => c.Id == fury.Id), strong);
        }
    }
}
