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
    public sealed class PveBattleSkillTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string pool, string enemy = "monster_006")
        {
            var p = new PlayerState(0, pool) { Fame = 3 };
            p.Skills.Add(CardJsonLoader.ParseHeroSkills(File.ReadAllText(Path.Combine(Folder, "skill.json")), pool)[2]);
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000" }) p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            p.Mana.AddCrystal(ManaColor.Blue);
            return new PveBattleSession(p, new[] { CardJsonLoader.LoadMonsters().Single(m => m.Id == enemy) }, CardJsonLoader.LoadBasicActions());
        }
        private void Melee(PveBattleSession s)
        {
            s.Advance(); s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_016"), "strong");
            s.Block(0); s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
        }

        [TestCase("hero_skill_pool_000", Element.Fire)]
        [TestCase("hero_skill_pool_007", Element.Ice)]
        public void ElementalSkillCombinesWithUnchangedPhysicalCardAndResolvesRealResistance(string pool, Element element)
        {
            var s = Create(pool); var skill = s.Player.Skills.Single(); Melee(s);
            s.UseSkill(skill, 1);
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.True);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Assert.That(s.Context.MeleePool, Is.EqualTo(2));
            s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_021"), "attack");
            var parts = s.Context.CombatPower.AvailableAttacks(Phase.Melee, new[] { 0 }, s.Enemies);
            Assert.That(parts.Single(a => a.Element == element).Value, Is.EqualTo(2));
            Assert.That(parts.Single(a => a.Element == Element.Physical).Value, Is.EqualTo(2));
            Assert.That(s.Context.AttackElementChange.ContainsKey(-1), Is.False);
            var result = s.Attack(0);
            Assert.That(result.EffectivePower, Is.EqualTo(3), "元素2 + 被物抗减半的普通2 = 3，不能整笔变色");
            Assert.That(result.Kills, Is.EqualTo(1)); Assert.That(s.Player.Fame, Is.EqualTo(7));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(SkillActions.IsUsed(s.Player, skill), Is.True, "战末不能当作回合结束重置");
            var sameTurn = new PveBattleSession(s.Player, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            sameTurn.Advance(); sameTurn.Advance(); sameTurn.Advance();
            Assert.Throws<InvalidOperationException>(() => sameTurn.UseSkill(skill, 1));
        }

        [TestCase("hero_skill_pool_000")]
        [TestCase("hero_skill_pool_007")]
        public void PhysicalChoiceAndActualSidewaysCardWinWithoutChangingCardIdentity(string pool)
        {
            var s = Create(pool, "monster_032"); Melee(s); s.UseSkill(s.Player.Skills.Single(), 0);
            var material = s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"); s.PlaySideways(material);
            Assert.That(s.Context.MeleePool, Is.EqualTo(3)); Assert.That(s.Player.Deck.PlayedCards.Contains(material), Is.True);
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(3)); Assert.That(s.Player.Fame, Is.EqualTo(5));
            s.Advance(); Assert.That(s.Won, Is.True);
        }

        [Test] public void InvalidTimingChoiceAndForeignInstanceDoNotConsumeSkillOrResources()
        {
            var s = Create("hero_skill_pool_007"); var skill = s.Player.Skills.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 1));
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False); Melee(s);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 2));
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(s.Player, s.Context, skill, 2));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[2], 1));
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False); Assert.That(s.Context.MeleePool, Is.Zero);
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(2)); Assert.That(s.Player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            s.UseSkill(skill, 1); Assert.That(s.Context.MeleePool, Is.EqualTo(2));
        }
    }
}
