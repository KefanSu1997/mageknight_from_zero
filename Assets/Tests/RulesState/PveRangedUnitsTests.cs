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
    public sealed class PveRangedUnitsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(bool savage, string enemy = "monster_032", bool green = true)
        {
            var p = new PlayerState(0, "原远程部队");
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, savage ? "army_2_008" : "army_2_012")));
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            if (savage && green) p.Mana.AddCrystal(ManaColor.Green);
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_011" })
                p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
        private void Finish(PveBattleSession s, int fame)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame));
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.Units.Single().Wounds, Is.Zero);
            Assert.That(s.Player.Units.Single().IsReady, Is.False);
        }

        [TestCase(true, false)] [TestCase(true, true)] [TestCase(false, false)] [TestCase(false, true)]
        public void OrdinaryAttackAndBlockUseRealConsumers(bool savage, bool block)
        {
            var s = Create(savage); var unit = s.Player.Units.Single(); s.Advance();
            if (block) { s.UseUnit(unit, 0, 1); Sideways(s); }
            else Play(s, 16, "strong");
            Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(block ? 4 : 5));
            s.Block(0); s.Advance(); s.Advance();
            if (block) Play(s, 21, "strong"); else s.UseUnit(unit, 0, 0);
            Assert.That(s.Context.MeleePool, Is.EqualTo(block ? 4 : 3));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 0, 0));
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); Finish(s, 2);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(block ? 0 : 1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(block ? 1 : 0));
            Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(savage ? 1 : 0));
        }

        [TestCase(true)] [TestCase(false)]
        public void PrintedSiegeOrRangedDefeatsBeforeEnemyAttack(bool savage)
        {
            var s = Create(savage, savage ? "monster_017" : "monster_032"); var unit = s.Player.Units.Single();
            s.UseUnit(unit, 1);
            Assert.That(s.Context.CombatPower.Attacks.Single().Type, Is.EqualTo(savage ? AttackType.Siege : AttackType.Ranged));
            Assert.That(s.Context.SiegePool, Is.EqualTo(savage ? 4 : 0));
            Assert.That(s.Context.RangedPool, Is.EqualTo(savage ? 0 : 2));
            Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1));
            if (!savage) Play(s, 11, "base");
            var result = s.Attack(0); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(result.EffectivePower, Is.EqualTo(savage ? 4 : 3)); Finish(s, savage ? 3 : 2);
        }

        [TestCase(true)] [TestCase(false)]
        public void SpecialAttackCanAlsoBeSpentInMelee(bool savage)
        {
            var s = Create(savage); s.Advance(); Play(s, 16, "strong"); s.Block(0); s.Advance(); s.Advance();
            s.UseUnit(s.Player.Units.Single(), 1); if (!savage) Play(s, 11, "base");
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); Finish(s, 2);
        }

        [TestCase(true)] [TestCase(false)]
        public void WrongPhaseDoesNotExhaustOrGrantAttack(bool savage)
        {
            var s = Create(savage); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 0, 0));
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1));
            Assert.That(unit.IsReady, Is.True); Assert.That(s.Context.SiegePool + s.Context.RangedPool + s.Context.MeleePool, Is.Zero);
            Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(ManaColor.Green), Is.EqualTo(savage ? 1 : 0));
        }

        [Test] public void MissingGreenDoesNotExhaustOrSpendOtherColors()
        {
            var s = Create(true, green: false); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1));
            Assert.That(unit.IsReady, Is.True); Assert.That(s.Context.SiegePool, Is.Zero);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
        }
        [Test] public void RangedCannotBypassFortificationOrSpendOnRejectedTarget()
        {
            var s = Create(false, "monster_017"); s.UseUnit(s.Player.Units.Single(), 1); Play(s, 11, "strong");
            Assert.That(s.Context.RangedPool, Is.EqualTo(5)); Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            Assert.That(s.Context.RangedPool, Is.EqualTo(5)); Assert.That(s.Player.Fame, Is.Zero);
            Assert.That(s.Context.DefeatedEnemyIndices, Is.Empty);
        }
    }
}
