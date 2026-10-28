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
    public sealed class PveForgeSkillsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(int index, bool capped = false)
        {
            var p = new PlayerState(0, "高蒂克斯") { Fame = 3 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills("hero_skill_pool_005")[index]);
            if (index == 3) p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_2_008")));
            if (capped) p.Mana.AddCrystal(ManaColor.Blue, 3);
            foreach (int n in new[] { 16, 21, 0, 7 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == (index == 3 ? "monster_017" : "monster_032")), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), "strong");

        [TestCase(2, ManaColor.White, false)] [TestCase(3, ManaColor.Green, false)]
        [TestCase(4, ManaColor.Red, false)] [TestCase(4, ManaColor.Red, true)]
        public void PrintedForgeManaPaysRealCardsOrUnitAndCompletesBattle(int index, ManaColor color, bool capped)
        {
            var s = Create(index, capped); var p = s.Player; var skill = p.Skills.Single();
            Assert.That(skill.SourceId, Is.EqualTo("hero_skill_pool_005:" + index.ToString("00")));
            Assert.That(skill.Frequency, Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.That(skill.PrintedRule, Does.Contain("蓝色魔晶"));
            s.UseSkill(skill, 0);
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(capped ? 3 : 1));
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.EqualTo(capped ? 1 : 0));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(1));
            if (index == 2) Play(s, 7);
            else if (index == 3) s.UseUnit(p.Units.Single(), 1);
            else
            {
                s.Advance(); Play(s, 16); Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(5));
                s.Block(0); s.Advance(); s.Advance(); Play(s, 21);
                Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(capped ? 3 : 0), "溢出蓝标记先支付，不能消耗蓝晶");
                Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue), Is.Zero);
            }
            Assert.That(p.Mana.Tokens.GetValueOrDefault(color), Is.Zero);
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(index == 2 ? 3 : 4));
            Assert.That(result.Kills, Is.EqualTo(1));
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(p.Wounds, Is.Zero);
            Assert.That(p.Fame, Is.EqualTo(index == 3 ? 6 : 5));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidChoiceOrUnownedSkillDoesNotGrantManaOrConsumeUse(int index)
        {
            var s = Create(index); var skill = s.Player.Skills.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 1));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(CardJsonLoader.LoadHeroSkills("hero_skill_pool_005")[index], 0));
            Assert.That(s.Player.Mana.GetTotal(), Is.Zero);
            Assert.That(SkillActions.IsUsed(s.Player, skill), Is.False);
        }

        [Test] public void ActualNewTurnDoesNotResetForgeButNewRoundDoes()
        {
            var s = Create(4); var p = s.Player; var skill = p.Skills.Single(); s.UseSkill(skill, 0);
            new PlayerTurnEngine().StartTurn(p, DayPart.Day);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            new GameEngine(new[] { p }, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); s.UseSkill(skill, 0);
            Assert.That(p.Mana.Crystals[ManaColor.Blue], Is.EqualTo(2));
        }
    }
}
