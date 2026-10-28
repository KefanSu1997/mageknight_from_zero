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
    public sealed class PvePersistentFlipSkillsTests
    {
        [SetUp] public void Data()=>CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
        private SkillCard Own(PlayerState p,int index){var s=CardJsonLoader.LoadHeroSkills("hero_skill_pool_003")[index];p.Skills.Add(s);return s;}
        [TestCase(0,2,false)] [TestCase(1,4,true)]
        public void FrenzyActualAttackAndFlipPersistsUntilSuccessfulRest(int option,int power,bool flipped)
        {
            var p=new PlayerState{Armor=2};var skill=Own(p,2);
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
            var b=new PveBattleSession(p,new[]{e},CardJsonLoader.LoadBasicActions());b.Advance();b.Advance();b.Advance();
            b.UseSkill(skill,option);Assert.That(b.Context.MeleePool,Is.EqualTo(power));
            Assert.That(p.FlippedSkills.Contains(skill.SourceId),Is.EqualTo(flipped));
            Assert.Throws<InvalidOperationException>(()=>b.UseSkill(skill,0));
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);
            Assert.That(SkillActions.IsUsed(p,skill),Is.EqualTo(flipped));
            new PveJourneySession(p,CardJsonLoader.LoadBasicActions()).PrepareRound();Assert.That(SkillActions.IsUsed(p,skill),Is.EqualTo(flipped));
            var ordinary=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(ordinary);
            var c=new ActionContext{MovedThisTurn=true};
            Assert.Throws<InvalidOperationException>(()=>RestActions.Rest(p,c,ordinary,Array.Empty<DeedCard>()));
            Assert.That(p.FlippedSkills.Contains(skill.SourceId),Is.EqualTo(flipped));
            c.MovedThisTurn=false;RestActions.Rest(p,c,ordinary,Array.Empty<DeedCard>());
            Assert.That(p.FlippedSkills.Contains(skill.SourceId),Is.False);Assert.That(c.RestedThisTurn,Is.True);
        }
        [TestCase(ManaColor.Red)] [TestCase(ManaColor.Blue)] [TestCase(ManaColor.Green)]
        [TestCase(ManaColor.White)] [TestCase(ManaColor.Gold)] [TestCase(ManaColor.Black)]
        public void ShamanExactTokenFlipsAndOnlyActionRestores(ManaColor color)
        {
            var p=new PlayerState();var skill=Own(p,3);var c=new ActionContext();
            SkillActions.Use(p,c,skill,(int)color);Assert.That(p.Mana.Tokens[color],Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.Count,Is.Zero);Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            new PlayerTurnEngine().StartTurn(p,DayPart.Day);new PveJourneySession(p,CardJsonLoader.LoadBasicActions()).PrepareRound();Assert.That(SkillActions.IsUsed(p,skill),Is.True);
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,(int)color));
            c.InBattle=true;Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,100));c.InBattle=false;
            c.TurnActionCommitted=true;Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,100));c.TurnActionCommitted=false;
            SkillActions.Use(p,c,skill,100);Assert.That(c.TurnActionCommitted,Is.True);Assert.That(SkillActions.IsUsed(p,skill),Is.False);
            SkillActions.Use(p,c,skill,(int)color);Assert.That(p.Mana.Tokens[color],Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>SkillActions.Use(p,c,skill,100));
        }
        [Test] public void ShamanJourneyPaysOriginalCardAndRestDoesNotResetItsFlip()
        {
            var p=new PlayerState();var skill=Own(p,3);var c=new DeedCard("basic_card_021",CardType.Action);p.Deck.Hand.Add(c);
            var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions());j.SelectTactic("day_1");j.BeginTurn();
            j.UseOutsideSkill(skill,(int)ManaColor.Red);
            Assert.That(j.OutsideSkillChoices(skill).Single().Option,Is.EqualTo(100));
            var e=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");var b=j.BeginBattle(new[]{e});
            b.Advance();b.Advance();b.Advance();b.Play(c,"strong");
            Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Red),Is.Zero);
            Assert.That(b.Context.MeleePool,Is.EqualTo(4));
            var restCard=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(restCard);
            RestActions.Rest(p,new ActionContext(),restCard,Array.Empty<DeedCard>());
            Assert.That(p.FlippedSkills.Contains(skill.SourceId),Is.True);
        }
    }
}

