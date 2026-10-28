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
    public sealed class PveManaSkillsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(int kind)
        {
            var p = new PlayerState { Fame = 3 };
            p.Skills.Add(CardJsonLoader.LoadHeroSkills(kind < 2 ? "hero_skill_pool_000" : "hero_skill_pool_002")[kind == 3 ? 5 : 4]);
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, kind == 0 ? "army_003" : kind == 1 ? "army_001" : "army_2_008")));
            if (kind == 0) p.Mana.AddCrystal(ManaColor.Blue, 1);
            foreach (int n in new[] { 16, 21, 0, 7 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            var monster = CardJsonLoader.LoadMonsters().Single(m => m.Id == (kind == 1 ? "monster_017" : "monster_032"));
            return new PveBattleSession(p, kind == 1 ? new[] { monster } : new[] { monster, monster }, CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), "strong");

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void OriginalCrystalAndTokenActuallyPayCardsAndUnitsToWin(int kind)
        {
            var s = Create(kind); var p = s.Player; var skill = p.Skills.Single(); int option = kind == 1 ? 1 : 0;
            Assert.That(skill.SourceId, Is.EqualTo(kind < 2 ? "hero_skill_pool_000:04" : "hero_skill_pool_002:0" + (kind == 3 ? 5 : 4)));
            Assert.That(skill.Frequency, Is.EqualTo(SkillFrequency.OncePerRound));
            Assert.That(skill.PrintedRule, Does.Contain("魔晶"));
            s.UseSkill(skill, option);
            var crystal = kind < 2 ? ManaColor.Red : kind == 2 ? ManaColor.Green : ManaColor.White;
            var token = kind == 0 ? ManaColor.Red : kind == 1 ? ManaColor.Black : kind == 2 ? ManaColor.White : ManaColor.Green;
            Assert.That(p.Mana.Crystals[crystal], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[token], Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, option));
            Assert.That(p.Mana.Crystals[crystal], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[token], Is.EqualTo(1));
            if (kind == 0)
            {
                s.UseUnit(p.Units[0], 1); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red), Is.Zero);
                Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(6));
                s.Advance(); Play(s, 16); s.Block(1); s.Advance(); s.Advance(); Play(s, 21);
                Assert.That(p.Mana.Crystals[ManaColor.Red], Is.Zero); Assert.That(s.Attack(1).EffectivePower, Is.EqualTo(4));
            }
            else if (kind == 1)
            {
                s.UseUnit(p.Units[0], 2, 1); Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Black), Is.Zero);
                Assert.That(s.Context.AllAttacksSiege, Is.True); Play(s, 21);
                Assert.That(p.Mana.Crystals[ManaColor.Red], Is.Zero); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(4));
            }
            else
            {
                Play(s, 7); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(3));
                s.UseUnit(p.Units[0], 1); Assert.That(s.Attack(1).EffectivePower, Is.EqualTo(4));
                Assert.That(p.Mana.Tokens.GetValueOrDefault(token), Is.Zero); Assert.That(p.Mana.Crystals[crystal], Is.Zero);
            }
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(p.Wounds, Is.Zero); Assert.That(p.Fame, Is.EqualTo(kind == 1 ? 6 : 7));
            Assert.That(p.Units[0].IsReady, Is.False); Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, option));
        }

        [TestCase(0, ManaColor.Red, ManaColor.Red)] [TestCase(2, ManaColor.Green, ManaColor.White)] [TestCase(3, ManaColor.White, ManaColor.Green)]
        public void CappedCrystalBecomesExtraTokenWithoutLosingPrintedToken(int kind, ManaColor crystal, ManaColor token)
        {
            var s = Create(kind); var p = s.Player; p.Mana.AddCrystal(crystal, 3); s.UseSkill(p.Skills.Single(), 0);
            Assert.That(p.Mana.Crystals[crystal], Is.EqualTo(3));
            Assert.That(p.Mana.Tokens[crystal], Is.EqualTo(crystal == token ? 2 : 1));
            Assert.That(p.Mana.Tokens[token], Is.EqualTo(crystal == token ? 2 : 1));
        }

        [TestCase(0)] [TestCase(2)] [TestCase(3)]
        public void InvalidChoiceOrForeignInstanceCannotProduceManaOrConsumeSkill(int kind)
        {
            var s = Create(kind); var p = s.Player; var skill = p.Skills.Single(); int before = p.Mana.GetTotal();
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p, s.Context, skill, -1));
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p, s.Context, skill, 2));
            var foreign = CardJsonLoader.LoadHeroSkills(kind < 2 ? "hero_skill_pool_000" : "hero_skill_pool_002")[kind == 3 ? 5 : 4];
            Assert.Throws<InvalidOperationException>(() => SkillActions.Use(p, s.Context, foreign));
            Assert.That(p.Mana.GetTotal(), Is.EqualTo(before)); Assert.That(SkillActions.IsUsed(p, skill), Is.False);
        }

        [TestCase(0)] [TestCase(2)] [TestCase(3)]
        public void TrueNewTurnKeepsFlipAndTrueNewRoundRestoresSkill(int kind)
        {
            var s = Create(kind); var p = s.Player; var skill = p.Skills.Single(); s.UseSkill(skill, 0);
            new PlayerTurnEngine().StartTurn(p, DayPart.Day);
            Assert.Throws<InvalidOperationException>(() => s.UseSkill(skill, 0));
            new GameEngine(new[] { p }, 3).StartNewRound(Array.Empty<TacticCard>());
            Assert.That(SkillActions.IsUsed(p, skill), Is.False); s.UseSkill(skill, 0);
            Assert.That(p.Mana.Crystals[kind == 0 ? ManaColor.Red : kind == 2 ? ManaColor.Green : ManaColor.White], Is.EqualTo(2));
        }
    }
}
