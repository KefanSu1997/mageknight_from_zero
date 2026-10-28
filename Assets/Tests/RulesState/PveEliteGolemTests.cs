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
    public sealed class PveEliteGolemTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string id, string enemy, bool mana = true)
        {
            var p = new PlayerState(); p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, id)));
            if (mana) { p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2); }
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
        private void End(PveBattleSession s, int fame)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame)); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.That(s.Player.Units.Single().Wounds, Is.Zero);
        }

        [TestCase(0, "monster_032", Element.Physical, 3, 2)]
        [TestCase(1, "monster_017", Element.Fire, 5, 3)]
        [TestCase(2, "monster_017", Element.Ice, 5, 3)]
        public void CatapultPaysPrintedColorsAndSiegeIgnoresFortification(int ability, string enemy, Element element, int value, int fame)
        {
            var s = Create("army_004", enemy); var u = s.Player.Units.Single(); s.UseUnit(u, ability);
            Assert.That(u.Card.Level, Is.EqualTo(3)); Assert.That(u.Card.Armor, Is.EqualTo(4));
            Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.City | RecruitLocation.Keep));
            var attack = s.Context.CombatPower.Attacks.Single(); Assert.That(attack.Type, Is.EqualTo(AttackType.Siege));
            Assert.That(attack.Element, Is.EqualTo(element)); Assert.That(attack.Value, Is.EqualTo(value));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(ability == 1 ? 1 : 2));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(ability == 2 ? 1 : 2));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, ability));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(value)); End(s, fame);
        }

        [TestCase("army_006", false)] [TestCase("army_006", true)]
        [TestCase("army_012", false)] [TestCase("army_012", true)]
        public void GolemsUseOriginalPhysicalAttackAndBlock(string id, bool block)
        {
            var s = Create(id, "monster_032"); var u = s.Player.Units.Single(); s.Advance();
            if (block) { s.UseUnit(u, 0, 1); Sideways(s); }
            else Play(s, 16, "strong");
            s.Block(0); s.Advance(); s.Advance();
            if (block) Play(s, 21, "strong"); else s.UseUnit(u, 0);
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(block ? 4 : 3)); End(s, 2);
            Assert.That(u.IsReady, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void GolemElementalAttackConsumesPrintedManaAndRetainsType(bool ice)
        {
            var s = Create(ice ? "army_012" : "army_006", ice ? "monster_029" : "monster_004"); var u = s.Player.Units.Single();
            Assert.That(s.Context.DayPart, Is.EqualTo(DayPart.Day));
            if (ice) { s.Advance(); Play(s, 16, "strong"); Sideways(s); s.Block(0); s.Advance(); s.Advance(); }
            s.UseUnit(u, 1); var attack = s.Context.CombatPower.Attacks.Single();
            Assert.That(attack.Element, Is.EqualTo(ice ? Element.Ice : Element.Fire));
            Assert.That(attack.Type, Is.EqualTo(ice ? AttackType.Melee : AttackType.Ranged));
            Assert.That(s.Player.Mana.Crystals[ice ? ManaColor.Blue : ManaColor.Red], Is.EqualTo(ice ? 0 : 1));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(ice ? 6 : 4)); End(s, 5);
        }

        [TestCase("army_006", "monster_029")] [TestCase("army_012", "monster_004")]
        [TestCase("army_006", "monster_032")] [TestCase("army_012", "monster_032")]
        public void PrintedElementAndPhysicalResistanceAbsorbRealEnemyAttackWithoutWound(string id, string enemy)
        {
            var s = Create(id, enemy); var u = s.Player.Units.Single();
            Assert.That(u.Card.Abilities, Does.Contain(Ability.PhysicalResist));
            Assert.That(u.Card.Abilities, Does.Contain(id == "army_006" ? Ability.FireResist : Ability.IceResist));
            Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Keep | RecruitLocation.MageTower));
            s.Advance(); s.Advance(); s.PlanDamage(0, 0, u); s.Advance();
            Assert.That(u.Wounds, Is.Zero); Assert.That(s.Player.Wounds, Is.Zero); Assert.That(u.IsReady, Is.True);
            Assert.That(s.Context.DamageAssignedUnits.Contains(u), Is.True);
            s.UseUnit(u, 0);
            if (enemy != "monster_032") { Play(s, 21, "strong"); Play(s, 11, "base"); }
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); End(s, enemy == "monster_032" ? 2 : 5);
        }

        [TestCase("army_004", 1)] [TestCase("army_004", 2)] [TestCase("army_006", 1)] [TestCase("army_012", 1)]
        public void MissingPrintedManaDoesNotExhaustOrCreatePower(string id, int ability)
        {
            var s = Create(id, "monster_032", false); var u = s.Player.Units.Single();
            if (id == "army_012") { s.Advance(); s.Advance(); s.PlanDamage(0, 0, u); s.Advance(); }
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, ability));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Context.CombatPower.Attacks, Is.Empty);
        }

        [Test] public void IceGolemMeleeAttackCannotBeUsedInRangedPhase()
        {
            var s = Create("army_012", "monster_032"); var u = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(2));
            Assert.That(s.Context.CombatPower.Attacks, Is.Empty);
        }
    }
}
