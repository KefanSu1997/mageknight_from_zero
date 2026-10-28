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
    public sealed class PveBattleSessionTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static PveBattleSession Create(string[] cards, params string[] monsters)
        {
            var player = new PlayerState(0, "诺洛瓦斯");
            foreach (string id in cards) player.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.White }) player.Mana.AddCrystal(color);
            var catalog = CardJsonLoader.LoadMonsters();
            return new PveBattleSession(player, monsters.Select(id => catalog.Single(m => m.Id == id)), CardJsonLoader.LoadBasicActions());
        }
        private static DeedCard Card(PveBattleSession s, string id) => s.Player.Deck.Hand.First(c => c.Id == id);

        [Test] public void SelectedOriginalBlocksPreserveOtherPhysicalCardForSecondEnemy()
        {
            var s = Create(new[] { "basic_card_016", "basic_card_016" }, "monster_032", "monster_032");
            s.Player.Mana.AddCrystal(ManaColor.Blue);
            var cards = s.Player.Deck.Hand.ToArray();
            s.Advance(); s.Play(cards[0], "strong"); s.Play(cards[1], "strong");
            var sources = s.Context.CombatPower.AvailableBlocks(0, 0, s.Enemies[0]);
            Assert.That(sources.Length, Is.EqualTo(2));
            Assert.That(ReferenceEquals(sources[0].SourceCard, cards[0]), Is.True);
            Assert.That(ReferenceEquals(sources[1].SourceCard, cards[1]), Is.True);
            Assert.That(s.BlockSelectedPreview(0, new[] { sources[0] }).Effective, Is.EqualTo(5));
            s.BlockSelected(0, new[] { sources[0] });
            Assert.That(s.Context.BlockPool, Is.EqualTo(5));
            var remaining = s.Context.CombatPower.AvailableBlocks(1, 0, s.Enemies[1]);
            Assert.That(remaining.Length, Is.EqualTo(1));
            Assert.That(ReferenceEquals(remaining[0], sources[1]), Is.True);
            Assert.Throws<InvalidOperationException>(() => s.BlockSelected(1, new[] { sources[0] }));
            s.BlockSelected(1, remaining);
            Assert.That(s.Context.BlockPool, Is.Zero);
            s.Advance(); s.Advance();
            Assert.That(s.Player.Wounds, Is.Zero);
        }

        [Test] public void SelectedBlockRejectsDuplicateCloneAndInsufficientWithoutConsumingActualSource()
        {
            var s = Create(new[] { "basic_card_016" }, "monster_032");
            s.Advance(); s.Play(Card(s, "basic_card_016"), "block");
            var source = s.Context.CombatPower.AvailableBlocks(0, 0, s.Enemies[0]).Single();
            Assert.Throws<InvalidOperationException>(() => s.BlockSelected(0, new[] { source, source }));
            Assert.Throws<InvalidOperationException>(() => s.BlockSelected(0, new[] { source with { } }));
            Assert.Throws<InvalidOperationException>(() => s.BlockSelected(0, new[] { source }));
            Assert.That(s.Context.BlockPool, Is.EqualTo(2));
            Assert.That(ReferenceEquals(s.Context.CombatPower.AvailableBlocks(0, 0, s.Enemies[0]).Single(), source), Is.True);
            Assert.That(s.IsBlocked(0), Is.False);
            Assert.That(s.ReservedBlocks, Is.Empty);
        }

        [Test] public void OriginalDeterminationAndRageWinWithExactPaymentAndInstances()
        {
            var s = Create(new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_014", "basic_card_008" }, "monster_032");
            var block = Card(s, "basic_card_016"); var attack = Card(s, "basic_card_021");
            s.Advance(); s.Play(block, "strong");
            Assert.That(s.Context.BlockPool, Is.EqualTo(5)); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.Zero);
            s.Block(0); Assert.That(s.Context.BlockPool, Is.Zero);
            s.Advance(); Assert.That(s.Phase, Is.EqualTo(Phase.AssignDamage)); s.Advance();
            Assert.That(s.Player.Wounds, Is.Zero); s.Play(attack, "strong");
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(4)); Assert.That(result.RequiredPower, Is.EqualTo(3));
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(s.Player.Fame, Is.EqualTo(2));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.Zero); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Context.InBattle, Is.False);
            Assert.That(s.Player.Deck.PlayedCards[0], Is.SameAs(block)); Assert.That(s.Player.Deck.PlayedCards[1], Is.SameAs(attack));
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => s.Advance()); Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            Assert.That(s.Player.Fame, Is.EqualTo(2));
        }

        [Test] public void ReplacementDeckAndEnemiesRangedKillPreventsPoisonThenRealWoundsAndMelee()
        {
            var s = Create(new[] { "basic_card_007", "basic_card_011", "basic_card_021", "basic_card_016", "basic_card_000" }, "monster_006", "monster_007");
            s.Player.Mana.AddCrystal(ManaColor.Blue);
            Assert.That(s.Enemies[0].Abilities.Contains(Ability.PhysicalResist), Is.True, "原敌人标记护甲旁的物理抗性图标");
            s.Play(Card(s, "basic_card_007"), "strong"); s.Play(Card(s, "basic_card_011"), "strong");
            var ranged = s.Attack(0);
            Assert.That(ranged.PrintedPower, Is.EqualTo(6)); Assert.That(ranged.EffectivePower, Is.EqualTo(3));
            Assert.That(s.Player.Fame, Is.EqualTo(5)); Assert.That(s.Context.DefeatedEnemyIndices.Single(), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            s.Advance(); s.Play(Card(s, "basic_card_016"), "strong");
            Assert.Throws<InvalidOperationException>(() => s.Block(1)); // Five does not meet Swift's six.
            Assert.That(s.Context.BlockPool, Is.EqualTo(5));
            s.Advance(); s.Advance();
            Assert.That(s.Player.Wounds, Is.EqualTo(2)); // Swift attack 3 / original hero armor 2, round up.
            Assert.That(s.Player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
            Assert.That(s.Player.DiscardWounds, Is.Zero); // Poison enemy was killed before it attacked.
            s.Play(Card(s, "basic_card_021"), "strong"); s.Attack(1); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(8)); Assert.That(s.Player.Wounds, Is.EqualTo(2));
            Assert.That(s.Player.Deck.PlayedCards.Count, Is.EqualTo(4));
        }

        [Test] public void SwiftNeedsSixBlockAndFailedReservationDoesNotSpendIt()
        {
            var s = Create(new[] { "basic_card_016", "basic_card_000" }, "monster_007");
            s.Advance(); s.Play(Card(s, "basic_card_016"), "strong");
            Assert.That(s.BlockPreview(0), Is.EqualTo((5, 6)));
            Assert.Throws<InvalidOperationException>(() => s.Block(0)); Assert.That(s.Context.BlockPool, Is.EqualTo(5));
            s.PlaySideways(Card(s, "basic_card_000")); s.Block(0);
            Assert.Throws<InvalidOperationException>(() => s.Block(0));
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
            s.Advance(); Assert.That(s.Won, Is.False);
        }

        [Test] public void WrongPhaseAndMissingManaLeaveOriginalHandUnchanged()
        {
            var s = Create(new[] { "basic_card_021" }, "monster_032"); var card = s.Player.Deck.Hand.Single();
            Assert.Throws<InvalidOperationException>(() => s.Play(card, "strong"));
            Assert.Throws<InvalidOperationException>(() => s.PlaySideways(card));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1));
            s.Advance(); s.Advance(); s.Advance(); s.Player.Mana.Crystals.Clear();
            Assert.Throws<InvalidOperationException>(() => s.Play(card, "strong"));
            Assert.That(s.Player.Deck.Hand.Contains(card), Is.True); Assert.That(s.Player.Deck.PlayedCards.Count, Is.Zero);
            Assert.That(s.Context.MeleePool, Is.Zero);
        }

        [Test] public void InsufficientAttackDoesNotChipArmorAndFortifiedRejectsRanged()
        {
            // Original 017 ignores site fortification and has high armor 8; it is
            // not the fortified enemy this test claims to exercise. Use original 025.
            var s = Create(new[] { "basic_card_007", "basic_card_021", "basic_card_016" }, "monster_025");
            Assert.That(s.Enemies[0].Abilities,Does.Contain(Ability.Fortified));
            Assert.That(s.Enemies[0].Armor,Is.EqualTo(7));
            s.Play(Card(s, "basic_card_007"), "strong");
            Assert.Throws<InvalidOperationException>(() => s.Attack(0)); Assert.That(s.Context.RangedPool, Is.EqualTo(3));
            s.Advance(); Assert.That(s.Context.RangedPool, Is.Zero); s.Advance(); s.Advance();
            s.Play(Card(s, "basic_card_021"), "attack");
            Assert.That(s.AttackArmorPreview(0),Is.EqualTo(7));
            Assert.Throws<InvalidOperationException>(()=>s.Attack(0));
            Assert.That(s.Context.MeleePool,Is.EqualTo(2),"Rejected insufficient attack does not consume its actual contribution");
            Assert.That(s.Context.DefeatedEnemyIndices,Is.Empty);
            s.Play(Card(s, "basic_card_016"), "attack");
            Assert.That(s.AttackArmorPreview(0),Is.EqualTo(7),"Failed attack never chips printed armor");
            Assert.Throws<InvalidOperationException>(()=>s.Attack(0));
            Assert.That(s.Context.MeleePool,Is.EqualTo(4));Assert.That(s.Context.DefeatedEnemyIndices,Is.Empty);
            s.Advance(); Assert.That(s.Won, Is.False); Assert.That(s.Player.Fame, Is.Zero);
        }
    }
}
