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
    public sealed class PveUtemUnitsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string id, string enemy)
        {
            var p = new PlayerState(0, "原乌顿部队");
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, id)));
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            foreach (var name in new[] { "basic_card_016", "basic_card_021", "basic_card_000" }) p.Deck.Hand.Add(new DeedCard(name, CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, string id, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == id), choice);

        [TestCase("monster_032", 4, 4, 2)]
        [TestCase("monster_007", 8, 6, 3)]
        public void GuardsDoubleOnlyAgainstSwiftAndRealDefenseDoesNotDoubleAgain(string enemy, int effective, int need, int fame)
        {
            var s = Create("army_2_013", enemy); var unit = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, 1)); Assert.That(unit.IsReady, Is.True);
            s.Advance(); s.UseUnit(unit, 1);
            Assert.That(s.Context.BlockPool, Is.EqualTo(4)); Assert.That(s.BlockPreview(0), Is.EqualTo((effective, need)));
            s.Block(0); Assert.That(s.Context.BlockPool, Is.Zero);
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero); Assert.That(unit.Wounds, Is.Zero);
            Play(s, "basic_card_021", "strong"); s.Attack(0); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame)); Assert.That(unit.IsReady, Is.False);
        }

        [Test] public void GuardsAttackUsesActualUnitAndCardSidewaysContribution()
        {
            var s = Create("army_2_013", "monster_032"); var unit = s.Player.Units.Single();
            s.Advance(); Play(s, "basic_card_016", "strong"); s.Block(0); s.Advance(); s.Advance();
            s.UseUnit(unit, 0); Assert.That(s.Context.MeleePool, Is.EqualTo(2));
            s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(3)); s.Advance(); Assert.That(s.Won, Is.True);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void SwordsmenBothAbilitiesKeepChoiceExhaustionAndSelfWoundAtomic(bool strong, bool block)
        {
            string enemy = strong ? block ? "monster_007" : "monster_006" : "monster_032";
            var s = Create("army_2_014", enemy); var unit = s.Player.Units.Single();
            s.Advance();
            if (!block) { Play(s, "basic_card_016", "strong"); s.Block(0); s.Advance(); s.Advance(); }
            s.UseUnit(unit, strong ? 1 : 0, block ? 1 : 0);
            Assert.That(unit.Wounds, Is.EqualTo(strong ? 1 : 0)); Assert.That(unit.IsReady, Is.False);
            int expected = strong ? 6 : 3;
            Assert.That(block ? s.Context.BlockPool : s.Context.MeleePool, Is.EqualTo(expected));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(unit, strong ? 1 : 0, block ? 1 : 0));
            Assert.That(unit.Wounds, Is.EqualTo(strong ? 1 : 0), "拒绝重复发动不能再加创伤");
            if (strong) Assert.Throws<InvalidOperationException>(() => UnitDamageResolver.Validate(s.Player, s.Context, unit));
            if (block)
            {
                if (!strong) s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
                s.Block(0); s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
                Play(s, "basic_card_021", "strong");
            }
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(block ? 4 : 3));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(unit.Wounds, Is.EqualTo(strong ? 1 : 0));
            Assert.That(s.Player.Wounds, Is.Zero); Assert.That(s.Player.DiscardWounds, Is.Zero);
        }
    }
}
