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
    public sealed class PveTovakSkillsTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(int skill, string enemy, DayPart day = DayPart.Day)
        {
            var p = new PlayerState(0, "托瓦克") { Fame = 3 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills("hero_skill_pool_007")[skill]);
            p.Mana.AddCrystal(ManaColor.Red); p.Mana.AddCrystal(ManaColor.Blue);
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000", "basic_card_011" }) p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions(), new ActionContext { DayPart = day });
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));

        [TestCase(0, "monster_032", Element.Physical)]
        [TestCase(1, "monster_029", Element.Ice)]
        [TestCase(2, "monster_004", Element.Fire)]
        public void ShieldUsesIndependentElementContributionsAndRealBlock(int option, string enemy, Element element)
        {
            var s = Create(3, enemy); var skill = s.Player.Skills.Single(); s.Advance(); s.UseSkill(skill, option);
            Assert.That(s.Context.CombatPower.Blocks.Single().Element, Is.EqualTo(element));
            Assert.That(s.Context.BlockPool, Is.EqualTo(option == 0 ? 3 : 2));
            if (option == 0) Sideways(s);
            if (option == 1) Play(s, 16, "block");
            Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(option == 0 ? 4 : option == 1 ? 3 : 2));
            if (option == 1) Assert.That(s.Context.CombatPower.Blocks.Single(b => b.Element == Element.Physical).Value, Is.EqualTo(2));
            s.Block(0); s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.Zero);
            Play(s, 21, "strong");
            if (option == 1) { Play(s, 11, "strong"); Sideways(s); }
            if (option == 2) { Play(s, 16, "attack"); Play(s, 11, "base"); Sideways(s); }
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(4)); s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(option == 0 ? 5 : option == 1 ? 9 : 8), "强化利斧参与击杀额外获得1名望");
        }

        [TestCase(DayPart.Day, 1, "strong", 4, 6)]
        [TestCase(DayPart.Night, 2, "base", 3, 5)]
        public void SniperUsesActualDayPartAndCombinesWithOriginalAxe(DayPart day, int skillPower, string axeChoice, int total, int fame)
        {
            var s = Create(1, "monster_032", day); s.UseSkill(s.Player.Skills.Single(), 0);
            Assert.That(s.Context.RangedPool, Is.EqualTo(skillPower)); Play(s, 11, axeChoice);
            var attack = s.Attack(0); Assert.That(attack.EffectivePower, Is.EqualTo(total));
            Assert.That(s.Player.Fame, Is.EqualTo(fame), "强化原利斧本阶段击杀另奖1名望");
            s.Advance(); s.Advance(); s.Advance(); s.Advance(); Assert.That(s.Won, Is.True);
        }

        [TestCase("monster_006", 2)] [TestCase("monster_004", 2)] [TestCase("monster_029", 2)]
        [TestCase("monster_014", 5)]
        public void WeakeningCountsPhysicalFireIceButNotMagicImmunity(string enemy, int armor)
        {
            var s = Create(4, enemy); s.UseSkill(s.Player.Skills.Single(), 0, 0);
            Assert.That(CombatMath.EffectiveArmor(s.Enemies[0], 0, s.Context), Is.EqualTo(armor));
            if (enemy != "monster_006") return;
            s.Advance(); Play(s, 16, "strong"); s.Block(0); s.Advance(); s.Advance();
            Play(s, 21, "strong"); var attack = s.Attack(0);
            Assert.That(attack.EffectivePower, Is.EqualTo(2)); Assert.That(attack.RequiredPower, Is.EqualTo(2));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(7));
            Assert.That(s.Context.SkillArmorReduction(s.Enemies[0]), Is.Zero, "仅本场战斗减甲");
        }

        [Test] public void WeakeningKeepsTargetIdentityAndMinimumArmorAndRejectsInvalidTargets()
        {
            var s = Create(4, "monster_004"); var skill = s.Player.Skills.Single(); var enemy = s.Enemies[0];
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            s.Context.DefeatedEnemyIndices.Add(0); Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0, 0));
            s.Context.DefeatedEnemyIndices.Clear(); Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False);
            s.Context.ArmorReduction[0] = 3; s.UseSkill(skill, 0, 0);
            Assert.That(CombatMath.EffectiveArmor(enemy, 0, s.Context), Is.EqualTo(1));
            Assert.That(s.Context.SkillArmorReduction(enemy with { }), Is.Zero, "相同原数据的新敌人实例不能继承减甲");
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0, 0));
            Assert.That(s.Context.SkillArmorReduction(enemy), Is.EqualTo(2));
        }

        [TestCase(1)] [TestCase(3)]
        public void InvalidSkillOptionsDoNotConsumeTurnUse(int index)
        {
            var s = Create(index, "monster_032"); var skill = s.Player.Skills.Single();
            if (index == 3) s.Advance();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(s.Player, s.Context, skill, 9));
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False); Assert.That(s.Context.BlockPool + s.Context.RangedPool, Is.Zero);
        }
    }
}
