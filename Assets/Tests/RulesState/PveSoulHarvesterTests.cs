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
    public sealed class PveSoulHarvesterTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(params string[] enemies)
        {
            var p = new PlayerState();
            p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2);
            foreach (int n in new[] { 16, 16, 21, 21, 0, 11 })
                p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_019", CardType.Artifact));
            return new PveBattleSession(p, enemies.Select(id => CardJsonLoader.LoadMonsters().Single(m => m.Id == id)),
                CardJsonLoader.LoadBasicActions(), artifacts: CardJsonLoader.LoadItems());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.First(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
        private void Defend(PveBattleSession s)
        {
            s.Advance();
            for (int i = 0; i < s.Enemies.Count; i++)
            {
                Play(s, 16, "strong");
                if (s.Enemies[i].Id == "monster_029") Sideways(s);
                s.Block(i);
            }
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
        }
        private void Harvester(PveBattleSession s, bool burn)
        {
            var card = s.Player.Deck.Hand.Single(c => c.Id == "items_019");
            s.PlayArtifact(card, burn);
            Assert.That(burn ? s.Player.Deck.RemovedCards.Single() : s.Player.Deck.PlayedCards.Last(), Is.SameAs(card));
        }

        [TestCase("monster_029", ManaColor.Green)]
        [TestCase("monster_004", ManaColor.Blue)]
        [TestCase("monster_029", ManaColor.White)]
        public void NormalAttackChoosesEachLegalResistanceColorAndActualKill(string enemy, ManaColor color)
        {
            var s = Create(enemy); Defend(s); Harvester(s, false);
            Play(s, 21, "strong"); Play(s, 11, "base");
            var before = s.Player.Mana.Crystals.GetValueOrDefault(color);
            s.ChooseKillCrystal(0, color);
            Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(before));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(4));
            Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(color), Is.EqualTo(before + 1));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(5));
            Assert.That(s.Context.KillCrystalChoices, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void SecondAttackRewardsOnlyBurnMode(bool burn)
        {
            var s = Create("monster_032", "monster_032"); Defend(s); Harvester(s, burn);
            s.ChooseKillCrystal(0, ManaColor.White); Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.White], Is.EqualTo(1));
            Play(s, 21, "strong"); Assert.That(s.Attack(1).Kills, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.White], Is.EqualTo(burn ? 2 : 1));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(4));
            Assert.That(s.Context.KillCrystalChoices, Is.Empty); Assert.That(s.Context.CrystalsPerKill, Is.Zero);
        }

        [Test] public void OneAttackCanRewardDifferentChosenColorsForTwoEnemies()
        {
            var s = Create("monster_029", "monster_004"); Defend(s); Harvester(s, true);
            Play(s, 21, "strong"); Play(s, 21, "strong");
            s.ChooseKillCrystal(0, ManaColor.Green); s.ChooseKillCrystal(1, ManaColor.Blue);
            var result = s.Attack(0, 1);
            Assert.That(result.Kills, Is.EqualTo(2)); Assert.That(result.EffectivePower, Is.EqualTo(8));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Green], Is.EqualTo(1)); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.Zero);
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(10));
        }

        [Test] public void InvalidColorTargetAndTimingPreserveTheLastLegalChoice()
        {
            var s = Create("monster_029");
            Assert.Throws<InvalidOperationException>(() => s.ChooseKillCrystal(0, ManaColor.Green));
            Defend(s); Harvester(s, false); s.ChooseKillCrystal(0, ManaColor.Green);
            Assert.Throws<InvalidOperationException>(() => s.ChooseKillCrystal(0, ManaColor.Blue));
            Assert.Throws<InvalidOperationException>(() => s.ChooseKillCrystal(0, ManaColor.Gold));
            Assert.Throws<InvalidOperationException>(() => s.ChooseKillCrystal(4, ManaColor.White));
            Assert.That(s.Context.KillCrystalChoices.Single().Value, Is.EqualTo(ManaColor.Green));
            Assert.That(s.Context.MeleePool, Is.EqualTo(3)); Assert.That(s.Player.Fame, Is.Zero);
        }

        [Test] public void ChoiceRemapsOnlyToItsSurvivingEnemyAndExpiresAtTurnEnd()
        {
            var s = Create("monster_029", "monster_004"); Defend(s); Harvester(s, true);
            s.ChooseKillCrystal(0, ManaColor.Green); s.ChooseKillCrystal(1, ManaColor.Blue);
            s.Context.RemapEnemyModifiers(new Dictionary<int, int> { [1] = 0 });
            Assert.That(s.Context.KillCrystalChoices.Count, Is.EqualTo(1));
            Assert.That(s.Context.KillCrystalChoices[0], Is.EqualTo(ManaColor.Blue));
            s.Context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(s.Player, DayPart.Day, new ManaSource(1, () => DayPart.Day), s.Context);
            Assert.That(s.Context.KillCrystalChoices, Is.Empty);
        }
    }
}
