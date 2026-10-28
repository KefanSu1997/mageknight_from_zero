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
    public sealed class PveWrathHornTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(int seed, string enemy = "monster_017")
        {
            var p = new PlayerState(); p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_009", CardType.Artifact));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions(),
                new ActionContext { EffectRandom = new Random(seed) }, CardJsonLoader.LoadItems().Where(c => c.Id == "items_009"));
        }
        private static DeedCard Horn(PveBattleSession s) => s.Player.Deck.Hand.Single(c => c.Id == "items_009");
        private void End(PveBattleSession s, int wounds, int fame = 3)
        {
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame));
            Assert.That(s.Player.Wounds, Is.EqualTo(wounds));
            Assert.That(s.Player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(wounds));
        }

        [TestCase(0, ManaColor.Gold, 0)] [TestCase(6, ManaColor.Black, 1)]
        public void NormalHornRollsActualSixFaceDieAndPreservesCardIdentity(int seed, ManaColor face, int wounds)
        {
            var s = Create(seed); var horn = Horn(s); s.PlayHorn(horn, false, 0);
            Assert.That(s.Context.LastManaRolls, Is.EqualTo(new[] { face }));
            Assert.That(s.Context.SiegePool, Is.EqualTo(5)); Assert.That(s.Context.RangedPool, Is.Zero);
            Assert.That(s.Player.Deck.PlayedCards.Single(), Is.SameAs(horn)); Assert.That(s.Player.Deck.RemovedCards, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, false, 0));
            Assert.That(s.Context.LastManaRolls.Count, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); End(s, wounds);
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 1)] [TestCase(4, 2)] [TestCase(5, 3)]
        public void BurnBonusDeterminesExactRollCountActualWoundsAndPermanentRemoval(int bonus, int wounds)
        {
            var s = Create(2); var horn = Horn(s); s.PlayHorn(horn, true, bonus);
            var sequence = new[] { ManaColor.Gold, ManaColor.Green, ManaColor.Red, ManaColor.Black, ManaColor.Red };
            Assert.That(s.Context.LastManaRolls, Is.EqualTo(sequence.Take(bonus)));
            Assert.That(s.Context.SiegePool, Is.EqualTo(5 + bonus));
            Assert.That(s.Player.Deck.RemovedCards.Single(), Is.SameAs(horn)); Assert.That(s.Player.Deck.PlayedCards, Is.Empty);
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(4 + wounds));
            Assert.That(s.Context.CombatPower.Attacks.Single().Type, Is.EqualTo(AttackType.Siege));
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, true, bonus));
            Assert.That(s.Context.LastManaRolls.Count, Is.EqualTo(bonus)); End(s, wounds);
        }

        [Test] public void InvalidBonusForeignInstanceAndBlockPhaseRejectWithoutDrawingDice()
        {
            var s = Create(0); var horn = Horn(s);
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, false, 1));
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, true, -1));
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, true, 6));
            Assert.Throws<InvalidOperationException>(() => s.PlayHorn(new DeedCard("items_009", CardType.Artifact), true, 0));
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.PlayHorn(horn, true, 0));
            Assert.That(s.Context.LastManaRolls, Is.Empty); Assert.That(s.Context.SiegePool, Is.Zero);
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(5)); Assert.That(s.Player.Wounds, Is.Zero);
        }

        [Test] public void OriginalSiegeHornCanAlsoBeSpentInMelee()
        {
            var s = Create(0, "monster_032"); s.Advance();
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_016"), "strong"); s.Block(0); s.Advance(); s.Advance();
            s.PlayArtifact(Horn(s), false);
            Assert.That(s.Context.LastManaRolls, Is.EqualTo(new[] { ManaColor.Gold }));
            Assert.That(s.Context.SiegePool, Is.EqualTo(5)); End(s, 0, 2);
        }
    }
}
