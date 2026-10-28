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
    public sealed class PveIceSwordsmenTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string enemy = "monster_032", bool mana = true)
        {
            var p = new PlayerState(); p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_002")));
            if (mana) { p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2); }
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void End(PveBattleSession s, int fame)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame));
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.Units.Single().Wounds, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void PrintedPhysicalAttackAndBlockAreFive(bool block)
        {
            var s = Create(); var u = s.Player.Units.Single(); s.Advance();
            Assert.That(u.Card.Armor, Is.EqualTo(6)); Assert.That(u.Card.Level, Is.EqualTo(3));
            Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Keep | RecruitLocation.City));
            if (block) s.UseUnit(u, 0, 1); else Play(s, 16, "strong");
            Assert.That(s.Context.BlockPool, Is.EqualTo(5)); s.Block(0); s.Advance(); s.Advance();
            if (block) Play(s, 21, "strong"); else s.UseUnit(u, 0);
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(block ? 4 : 5)); End(s, 2);
        }
        [TestCase("monster_032", 2)] [TestCase("monster_029", 5)]
        public void BlueAbilityCancelsRealDamageAndReducesArmorToAtLeastOne(string enemy, int fame)
        {
            var s = Create(enemy); var u = s.Player.Units.Single(); s.Advance(); s.UseUnit(u, 1, target: 0);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1)); Assert.That(u.IsReady, Is.False);
            Assert.That(s.Context.IsAttackCancelled(0, 0), Is.True);
            Assert.That(CombatMath.EffectiveArmor(s.Enemies[0], 0, s.Context), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, target: 0));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
            Play(s, 16, "attack"); Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); End(s, fame);
            Assert.That(s.Context.UnitArmorReduction(s.Enemies[0]), Is.Zero); Assert.That(s.Context.SkipAttackIndices, Is.Empty);
        }
        [TestCase("monster_004")] [TestCase("monster_015")]
        public void IceResistanceRejectsBeforePaymentAndExhaustion(string enemy)
        {
            var s = Create(enemy); var u = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, target: 0));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(2)); Assert.That(u.IsReady, Is.True);
            Assert.That(s.Context.ArmorReduction, Is.Empty); Assert.That(s.Context.SkipAttackIndices, Is.Empty);
        }
        [Test] public void MissingFeeAndMissingTargetAreAtomic()
        {
            var s = Create(mana: false); var u = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, target: 0));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Context.SkipAttackIndices, Is.Empty);
            s.Player.Mana.AddCrystal(ManaColor.Blue, 1);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
        }
        [Test] public void AssignmentPhaseCannotRetroactivelyFreezeAnAttack()
        {
            var s = Create(); var u = s.Player.Units.Single(); s.Advance(); s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, target: 0));
            Assert.Throws<InvalidOperationException>(() => CardUnitActions.Use(s.Player, s.Context, u, 1, target: 0));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(2));
        }
    }
}
