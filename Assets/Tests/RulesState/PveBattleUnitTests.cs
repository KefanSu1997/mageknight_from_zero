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
    public sealed class PveBattleUnitTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string id, params string[] enemies)
        {
            var player = new PlayerState(0, "战斗快照");
            player.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, id)));
            player.Mana.AddCrystal(ManaColor.Red); player.Mana.AddCrystal(ManaColor.Blue);
            foreach (string card in new[] { "basic_card_016", "basic_card_021", "basic_card_000" })
                player.Deck.Hand.Add(new DeedCard(card, CardType.Action));
            return new PveBattleSession(player, enemies.Select(id => CardJsonLoader.LoadMonsters().Single(m => m.Id == id)), CardJsonLoader.LoadBasicActions());
        }

        [Test] public void ExhaustedGolemBlocksThenAbsorbsOtherAttackAndRealCardsFinishBattle()
        {
            var s = Create("army_2_001", "monster_032", "monster_032");
            var unit = s.Player.Units.Single();
            s.Advance(); s.UseUnit(unit, 2); // Printed blue mana: ice block 4.
            Assert.That(s.Context.BlockPool, Is.EqualTo(4));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.Zero);
            Assert.That(unit.IsReady, Is.False); s.Block(0);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 0, 1));
            s.Advance(); s.PlanDamage(1, 0, unit);
            Assert.That(unit.Wounds, Is.Zero, "Reservation must not assign damage early");
            Assert.That(s.DamagePlan.Single().Units.Single(), Is.SameAs(unit));
            s.Advance();
            Assert.That(unit.Wounds, Is.EqualTo(1)); // Physical resistance absorbs 3, then remaining 1 wounds the unit.
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(unit.CanActivate, Is.False);
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_021"), "strong"); s.Attack(1);
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_016"), "attack");
            s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000")); s.Attack(0); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(4));
            Assert.That(unit.IsReady, Is.False); Assert.That(unit.Wounds, Is.EqualTo(1));
        }

        [Test] public void RangedUnitPaysRedAndKillsOriginalPhysicalResistantEnemy()
        {
            var s = Create("army_003", "monster_006"); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 0));
            Assert.That(unit.IsReady, Is.True);
            s.UseUnit(unit, 1); Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.Zero);
            Assert.That(s.Context.RangedPool, Is.EqualTo(6));
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(6)); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1));
            s.Advance(); s.Advance(); s.Advance(); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(4)); Assert.That(s.Player.Wounds, Is.Zero);
        }

        [Test] public void InvalidActivationAndInsufficientManaAreAtomic()
        {
            var s = Create("army_2_001", "monster_032"); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 2));
            s.Advance(); s.Player.Mana.Crystals.Clear();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 2));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(new UnitState(unit.Card), 0, 1));
            Assert.That(unit.IsReady, Is.True); Assert.That(s.Context.BlockPool, Is.Zero);
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(3));
        }

        [Test] public void DamagePlansRejectDuplicatesBlockedTargetsAndForeignUnitsWithoutChangingReservation()
        {
            var s = Create("army_2_001", "monster_032", "monster_032"); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.PlanDamage(0, 0, unit));
            s.Advance(); s.Advance(); s.PlanDamage(0, 0, unit);
            Assert.Throws<InvalidOperationException>(() => s.PlanDamage(1, 0, unit));
            Assert.Throws<InvalidOperationException>(() => s.PlanDamage(0, 0, unit, unit));
            Assert.Throws<InvalidOperationException>(() => s.PlanDamage(1, 0, new UnitState(unit.Card)));
            Assert.That(s.DamagePlan.Single().TargetIndex, Is.Zero);
            s.PlanDamage(0, 0); Assert.That(s.DamagePlan.Count, Is.Zero);
            s.PlanDamage(1, 0, unit); s.Advance();
            Assert.That(s.Player.Wounds, Is.EqualTo(2)); Assert.That(unit.Wounds, Is.EqualTo(1));
            Assert.That(s.Player.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(2));
        }

        [Test] public void BlockedAttackCannotReserveUnitAndPoisonAddsTwoActualUnitWounds()
        {
            var s = Create("army_2_001", "monster_032", "monster_006"); var unit = s.Player.Units.Single();
            s.Advance(); s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_016"), "strong"); s.Block(0);
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.PlanDamage(0, 0, unit));
            s.PlanDamage(1, 0, unit); s.Advance();
            Assert.That(unit.Wounds, Is.EqualTo(2)); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.That(s.Player.DiscardWounds, Is.Zero);
        }
    }
}
