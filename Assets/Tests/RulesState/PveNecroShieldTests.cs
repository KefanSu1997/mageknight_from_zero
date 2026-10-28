using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class PveNecroShieldTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));

        private PveBattleSession Create(bool burn, bool split)
        {
            var player = new PlayerState();
            player.Mana.AddCrystal(ManaColor.Red); player.Mana.AddCrystal(ManaColor.Blue);
            foreach (int n in new[] { 16, 21, 0, 11 })
                player.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            player.Deck.Hand.Add(new DeedCard("items_020", CardType.Artifact));
            var ids = split ? burn ? new[] { "monster_015", "monster_032" } : new[] { "monster_032", "monster_032" }
                : new[] { burn ? "monster_029" : "monster_032" };
            return new PveBattleSession(player, ids.Select(id => CardJsonLoader.LoadMonsters().Single(m => m.Id == id)),
                CardJsonLoader.LoadBasicActions(), artifacts: CardJsonLoader.LoadItems());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void FourPrintedShieldModesReserveDistinctAttacksAndFinishBattle(bool burn, bool split)
        {
            var s = Create(burn, split); var shield = s.Player.Deck.Hand.Single(c => c.Id == "items_020");
            s.Advance(); s.PlayShield(shield, burn, split);
            Assert.That(burn ? s.Player.Deck.RemovedCards.Single() : s.Player.Deck.PlayedCards.Single(), Is.SameAs(shield));
            Assert.That(s.Context.BlockPool, Is.EqualTo(split ? burn ? 12 : 8 : burn ? 8 : 6));
            for (int target = 0; target < s.Enemies.Count; target++)
                for (int attack = 0; attack < s.Enemies[target].AttackCount; attack++)
                {
                    Assert.That(s.BlockPreview(target, attack).Effective, Is.EqualTo(split ? 4 : burn ? 8 : 6));
                    s.Block(target, attack);
                    int remaining = s.Context.BlockPool;
                    Assert.Throws<InvalidOperationException>(() => s.Block(target, attack));
                    Assert.That(s.Context.BlockPool, Is.EqualTo(remaining));
                }
            Assert.That(s.Context.BlockPool, Is.Zero);
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
            Play(s, 21, "strong");
            if (burn && !split)
            {
                Play(s, 11, "strong"); s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
            }
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            if (split)
            {
                Play(s, 16, "attack"); s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
                Assert.That(s.Attack(1).Kills, Is.EqualTo(1));
            }
            s.Advance(); Assert.That(s.Won, Is.True);
            Assert.That(s.Player.Fame, Is.EqualTo(burn ? split ? 5 : 6 : split ? 4 : 2));
            Assert.That(s.Context.BlockPool, Is.Zero); Assert.That(s.Player.Wounds, Is.Zero);
        }

        [Test] public void ShieldCannotBePlayedOutsideBlockOrUseForeignCard()
        {
            var s = Create(false, false); var card = s.Player.Deck.Hand.Single(c => c.Id == "items_020");
            Assert.Throws<InvalidOperationException>(() => s.PlayShield(card, true, true));
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.PlayShield(new DeedCard("items_020", CardType.Artifact), true, true));
            Assert.That(s.Player.Deck.Hand.Contains(card), Is.True); Assert.That(s.Player.Deck.RemovedCards, Is.Empty);
            Assert.That(s.Context.BlockPool, Is.Zero);
        }

        [Test] public void FormalSplitShieldCanBlockTwoAttacksFromSameOriginalEnemy()
        {
            var player = new PlayerState();
            var enemy = CardJsonLoader.LoadMonsters().Single(m => m.Id == "monster_015");
            var context = new ActionContext { InBattle = true, Enemies = new[] { enemy } };
            var shield = new DeedCard("items_020", CardType.Artifact);
            player.Deck.Hand.Add(shield);
            CardPlayActions.PlayArtifact(player, context, shield, CardJsonLoader.LoadItems().Single(c => c.Id == shield.Id), true, 1);
            Assert.That(CardCombatActions.Block(player, context, context.Enemies, 0, 0).Wounds, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => context.CombatPower.AvailableBlocks(0));
            Assert.That(CardCombatActions.Block(player, context, context.Enemies, 0, 1).Wounds, Is.Zero);
            Assert.That(player.Wounds, Is.Zero);
            Assert.That(context.BlockPool, Is.EqualTo(4));
            Assert.That(player.Deck.RemovedCards.Single(), Is.SameAs(shield));
        }
    }
}
