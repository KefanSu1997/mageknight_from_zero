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
    public sealed class PveSiegeHeroSkillsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string pool, string enemy, DayPart day = DayPart.Day)
        {
            var p = new PlayerState(0, pool) { Fame = 3 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills(pool)[pool == "hero_skill_pool_005" ? 0 : 1]);
            if (pool != "hero_skill_pool_002") p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_004")));
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions(), new ActionContext { DayPart = day });
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Finish(PveBattleSession s, int fame)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame)); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.That(SkillActions.IsUsed(s.Player, s.Player.Skills.Single()), Is.True);
        }

        [TestCase("hero_skill_pool_000", 0, Element.Physical)] [TestCase("hero_skill_pool_000", 1, Element.Fire)]
        [TestCase("hero_skill_pool_005", 0, Element.Physical)] [TestCase("hero_skill_pool_005", 1, Element.Ice)]
        public void OriginalSiegeSkillKeepsElementAndCompletesRealFortifiedKill(string pool, int option, Element element)
        {
            var s = Create(pool, "monster_017"); var skill = s.Player.Skills.Single(); s.UseSkill(skill, option);
            var part = s.Context.CombatPower.Attacks.Single(); Assert.That(part.Type, Is.EqualTo(AttackType.Siege));
            Assert.That(part.Element, Is.EqualTo(element)); Assert.That(part.Value, Is.EqualTo(1));
            Assert.That(s.Context.RangedPool, Is.Zero); Assert.That(s.Context.SiegePool, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, option));
            Assert.That(s.Context.SiegePool, Is.EqualTo(1));
            s.UseUnit(s.Player.Units.Single(), 0); Assert.That(s.Context.SiegePool, Is.EqualTo(4));
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(4)); Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            Finish(s, 6);
        }

        [TestCase("hero_skill_pool_000", Element.Fire)] [TestCase("hero_skill_pool_005", Element.Ice)]
        public void ElementalSiegeSkillIsUsableInMeleeWithoutRecoloringPhysicalCard(string pool, Element element)
        {
            var s = Create(pool, "monster_006"); s.Advance(); Play(s, 16, "strong"); s.Block(0); s.Advance(); s.Advance();
            s.UseSkill(s.Player.Skills.Single(), 1); Play(s, 21, "strong");
            Assert.That(s.Context.CombatPower.Attacks.Single(a => a.Type == AttackType.Siege).Element, Is.EqualTo(element));
            Assert.That(s.Context.CombatPower.Attacks.Single(a => a.Type == AttackType.Melee).Element, Is.EqualTo(Element.Physical));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(3), "元素1+物抗折半的普通4=3"); Finish(s, 7);
        }

        [TestCase(DayPart.Day, 2, "base", 3, 5)] [TestCase(DayPart.Night, 1, "strong", 4, 6)]
        public void DaySniperFollowsActualDayNightAndOriginalAxeReward(DayPart day, int power, string axe, int total, int fame)
        {
            var s = Create("hero_skill_pool_002", "monster_032", day); var skill = s.Player.Skills.Single();
            s.UseSkill(skill, 0); Assert.That(s.Context.RangedPool, Is.EqualTo(power));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Play(s, 11, axe); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(total)); Finish(s, fame);
        }

        [TestCase("hero_skill_pool_000")] [TestCase("hero_skill_pool_005")] [TestCase("hero_skill_pool_002")]
        public void InvalidOptionForeignSkillAndWrongPhasePreserveTurnUse(string pool)
        {
            var s = Create(pool, "monster_032"); var skill = s.Player.Skills.Single();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(s.Player, s.Context, skill, 9));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(CardJsonLoader.LoadHeroSkills(pool)[pool == "hero_skill_pool_005" ? 0 : 1], 0));
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False);
            Assert.That(s.Context.SiegePool + s.Context.RangedPool, Is.Zero);
        }

        [Test] public void SiegeSkillStaysUsedInSameTurnAndResetsOnlyOnRealTurnStart()
        {
            var s = Create("hero_skill_pool_005", "monster_017"); var skill = s.Player.Skills.Single();
            s.UseSkill(skill, 1); s.UseUnit(s.Player.Units.Single(), 0); s.Attack(0); Finish(s, 6);
            var next = new PveBattleSession(s.Player, CardJsonLoader.LoadMonsters().Where(m => m.Id == "monster_032"), CardJsonLoader.LoadBasicActions());
            Assert.Throws<InvalidOperationException>(() => next.UseSkill(skill, 0));
            new PlayerTurnEngine().StartTurn(s.Player, DayPart.Day);
            next.UseSkill(skill, 0); Assert.That(next.Context.SiegePool, Is.EqualTo(1));
        }
    }
}
