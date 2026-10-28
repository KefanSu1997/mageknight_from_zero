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
    public sealed class PveMonkUnitsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(bool northern, string enemy, bool mana = true)
        {
            var p = new PlayerState(0, "原僧侣部队");
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, northern ? "army_2_005" : "army_2_007")));
            if (mana) { p.Mana.AddCrystal(ManaColor.Red, 2); p.Mana.AddCrystal(ManaColor.Blue, 2); }
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_011" })
                p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));

        [TestCase(true, false, false)] [TestCase(true, false, true)]
        [TestCase(true, true, false)] [TestCase(true, true, true)]
        [TestCase(false, false, false)] [TestCase(false, false, true)]
        [TestCase(false, true, false)] [TestCase(false, true, true)]
        public void BothMonksResolvePrintedAttackBlockAndElementCosts(bool northern, bool elemental, bool block)
        {
            string enemy = elemental ? northern ? "monster_029" : "monster_004" : "monster_032";
            var s = Create(northern, enemy); var unit = s.Player.Units.Single();
            s.Advance();
            if (!block)
            {
                Play(s, 16, "strong");
                if (elemental && northern) Sideways(s);
                s.Block(0); s.Advance(); s.Advance();
            }
            int redBefore = s.Player.Mana.Crystals[ManaColor.Red], blueBefore = s.Player.Mana.Crystals[ManaColor.Blue];
            s.UseUnit(unit, elemental ? 1 : 0, block ? 1 : 0);
            Assert.That(unit.IsReady, Is.False); Assert.That(unit.Wounds, Is.Zero);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(blueBefore - (elemental && northern ? 1 : 0)));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(redBefore - (elemental && !northern ? 1 : 0)));
            Element expected = elemental ? northern ? Element.Ice : Element.Fire : Element.Physical;
            if (block) Assert.That(s.Context.CombatPower.Blocks.Single().Element, Is.EqualTo(expected));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, elemental ? 1 : 0, block ? 1 : 0));
            if (block)
            {
                if (!elemental) Sideways(s);
                Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(4));
                s.Block(0); s.Advance(); s.Advance(); Play(s, 21, "strong");
                if (elemental) { Play(s, 11, "strong"); Sideways(s); }
            }
            Assert.That(s.Player.Wounds, Is.Zero);
            var result = s.Attack(0);
            Assert.That(result.EffectivePower, Is.EqualTo(elemental || block ? 4 : 3));
            Assert.That(result.Kills, Is.EqualTo(1)); s.Advance(); Assert.That(s.Won, Is.True);
            Assert.That(s.Player.Fame, Is.EqualTo(elemental ? block ? 6 : 5 : 2));
            Assert.That(unit.IsReady, Is.False); Assert.That(unit.Wounds, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void MissingPrintedColorDoesNotExhaustOrGrantPower(bool northern)
        {
            var s = Create(northern, "monster_032", false); s.Advance(); var unit = s.Player.Units.Single();
            s.Player.Mana.AddCrystal(northern ? ManaColor.Red : ManaColor.Blue);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1, 1));
            Assert.That(unit.IsReady, Is.True); Assert.That(s.Context.BlockPool, Is.Zero);
            Assert.That(s.Player.Mana.Crystals[northern ? ManaColor.Red : ManaColor.Blue], Is.EqualTo(1));
        }
    }
}
