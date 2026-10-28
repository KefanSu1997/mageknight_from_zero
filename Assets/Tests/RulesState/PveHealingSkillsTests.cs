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
    public sealed class PveHealingSkillsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(int kind, string unit = null)
        {
            var p = new PlayerState { Fame = 3 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills(kind == 2 ? "hero_skill_pool_005" : "hero_skill_pool_004")[kind == 2 ? 1 : kind]);
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, unit ?? (kind == 2 ? "army_2_014" : "army_2_002"))));
            p.Mana.AddCrystal(ManaColor.Red, 1); p.Mana.AddCrystal(ManaColor.Blue, 1);
            foreach (int n in new[] {16, 21, 0, 11}) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
        }
        private void Win(PveBattleSession s, bool unitWound)
        {
            s.Advance(); s.Advance();
            if (unitWound) s.PlanDamage(0, 0, s.Player.Units.Single());
            s.Advance();
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_021"), "strong");
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); s.Advance(); Assert.That(s.Won, Is.True);
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void OriginalHealingIsStoredThenSpentOnActualHeroOrUnitWound(int kind, bool unitWound)
        {
            var s = Create(kind); Win(s, unitWound); var p = s.Player; var skill = p.Skills.Single();
            int wounds = p.Wounds; int units = p.Units[0].Wounds;
            Assert.That(skill.Frequency, Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.That(skill.PrintedRule, Does.Contain("战斗中除外"));
            Assert.That(wounds, Is.EqualTo(unitWound ? kind == 2 ? 0 : 1 : 2));
            Assert.That(units, Is.EqualTo(unitWound ? 1 : 0));
            s.UseSkill(skill, 0);
            Assert.That(p.Wounds, Is.EqualTo(wounds), "获得点数不应自动选择英雄");
            Assert.That(s.Context.Healing.Total, Is.EqualTo(kind == 2 ? 2 : 1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(kind == 0 ? 2 : 1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.White), Is.EqualTo(kind == 1 ? 1 : 0));
            bool wasReady = p.Units[0].IsReady;
            s.HealWound(unitWound ? 0 : -1);
            if (!unitWound && kind == 2) s.HealWound();
            Assert.That(s.Context.Healing.Total, Is.Zero); Assert.That(s.Context.Healing.Grail, Is.Zero);
            Assert.That(p.Units[0].Wounds, Is.Zero); Assert.That(p.Units[0].IsReady, Is.EqualTo(wasReady));
            Assert.That(p.Wounds, Is.EqualTo(unitWound ? wounds : kind == 2 ? 0 : 1));
            Assert.That(p.Deck.Hand.Count(c => c.Type == CardType.Wound), Is.EqualTo(p.Wounds));
            Assert.That(p.Fame, Is.EqualTo(5));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Assert.That(s.Context.Healing.Total, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CombatAndInvalidChoiceRejectBeforeCrystalsHealingOrMarker(int kind)
        {
            var s = Create(kind); var p = s.Player; var skill = p.Skills.Single();
            Assert.That(s.SkillChoices(skill), Is.Empty);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p, s.Context, skill));
            Assert.Throws<InvalidOperationException>(() => skill.Effect.Execute(p, s.Context));
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); Assert.That(s.Context.Healing.Total, Is.Zero);
            Win(s, false); int mana = p.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p, s.Context, skill, 1));
            Assert.That(p.Mana.GetTotal(), Is.EqualTo(mana)); Assert.That(p.Wounds, Is.EqualTo(2));
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); Assert.That(s.Context.Healing.Total, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ActualNewTurnKeepsRoundMarkerAndNewRoundResetsIt(int kind)
        {
            var s = Create(kind); Win(s, false); var p = s.Player; var skill = p.Skills.Single();
            s.UseSkill(skill, 0); new PlayerTurnEngine().StartTurn(p, DayPart.Day);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            new GameEngine(new[] {p}, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(SkillActions.IsUsed(p, skill), Is.False);
            s.UseSkill(skill, 0); Assert.That(SkillActions.IsUsed(p, skill), Is.True);
        }

        [TestCase(0, ManaColor.Blue)] [TestCase(1, ManaColor.White)]
        public void CrystalOverflowAndInsufficientUnitHealingPreserveRealResources(int kind, ManaColor color)
        {
            var s = Create(kind, "army_2_014"); Win(s, true); var p = s.Player;
            p.Mana.AddCrystal(color, 3 - p.Mana.Crystals.GetValueOrDefault(color));
            s.UseSkill(p.Skills.Single(), 0);
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(3)); Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.HealWound(0));
            Assert.That(s.Context.Healing.Total, Is.EqualTo(1)); Assert.That(p.Units[0].Wounds, Is.EqualTo(1));
        }
    }
}
