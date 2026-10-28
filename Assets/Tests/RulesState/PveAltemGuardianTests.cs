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
    public sealed class PveAltemGuardianTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string enemy = "monster_032", bool green = true)
        {
            var p = new PlayerState { Fame = 8 };
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder,"army_000")));
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder,"army_2_014")));
            p.Mana.AddCrystal(ManaColor.Red,1); p.Mana.AddCrystal(ManaColor.Blue,1);
            if (green) p.Mana.AddCrystal(ManaColor.Green,1);
            foreach (int n in new[] {16,21,0,11}) p.Deck.Hand.Add(new DeedCard("basic_card_"+n.ToString("000"),CardType.Action));
            return new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id==enemy),CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s,int n,string mode="strong") => s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_"+n.ToString("000")),mode);
        [TestCase(false,false)] [TestCase(true,false)] [TestCase(true,true)]
        public void PrintedAttackFiveOrBlockEightDoublesAgainstSwift(bool block,bool swift)
        {
            var s=Create(swift?"monster_017":"monster_032");var p=s.Player;var u=p.Units[0];
            Assert.That(u.Card.Level,Is.EqualTo(4));Assert.That(u.Card.Armor,Is.EqualTo(7));
            Assert.That(u.Card.InfluenceCost,Is.EqualTo(11));Assert.That(u.Card.RecruitLocationMask,Is.EqualTo(RecruitLocation.City));
            s.Advance();
            if(block)s.UseUnit(u,1);else Play(s,16);
            Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(block?swift?16:8:5));
            s.Block(0);s.Advance();s.Advance();
            if(block)Play(s,21);else s.UseUnit(u,0);
            Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(block?4:5));s.Advance();
            Assert.That(s.Won,Is.True);Assert.That(p.Wounds,Is.Zero);Assert.That(u.IsReady,Is.False);
            Assert.That(p.Fame,Is.EqualTo(swift?11:10));
        }

        [TestCase("monster_032",false,10)] [TestCase("monster_029",false,13)]
        [TestCase("monster_004",false,13)] [TestCase("monster_032",true,10)]
        public void GreenAllResistanceProtectsActualSelfOrCompanionAndSurvivesBattleEnd(string enemy,bool self,int fame)
        {
            var s=Create(enemy);var p=s.Player;var guard=p.Units[0];
            s.Advance();s.Advance();s.UseUnit(guard,2);s.PlanDamage(0,0,p.Units[self?0:1]);s.Advance();
            Assert.That(s.Context.UnitsResistAll,Is.True);Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);
            Assert.That(guard.IsReady,Is.False);Assert.That(p.Units.All(u=>u.Wounds==0),Is.True);Assert.That(p.Wounds,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(guard,2));
            s.UseUnit(p.Units[1],0);Play(s,21);Play(s,16,"attack");
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));s.Advance();Assert.That(s.Won,Is.True);
            Assert.That(p.Fame,Is.EqualTo(fame));Assert.That(s.Context.UnitsResistAll,Is.True);
            Assert.That(s.Context.IgnoreNextWound,Is.False,"守护者不是迷雾，不能附赠英雄免伤");
            new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,s.Context);
            Assert.That(s.Context.UnitsResistAll,Is.False);Assert.That(guard.IsReady,Is.False);
        }

        [TestCase(Element.Physical)] [TestCase(Element.Fire)] [TestCase(Element.Ice)] [TestCase(Element.ColdFire)]
        public void OriginalBuffUsesFormalResistanceAbsorptionForEveryElement(Element element)
        {
            var s=Create();var p=s.Player;s.UseUnit(p.Units[0],2);
            var remaining=UnitDamageResolver.Assign(p,s.Context,p.Units[1],s.Enemies[0],4,element);
            Assert.That(remaining,Is.Zero);Assert.That(p.Units[1].Wounds,Is.Zero);
            Assert.That(p.Units[0].Card.Abilities,Is.Empty,"不能把回合效果写入原部队固有抗性");
        }

        [Test] public void MissingGreenOrInvalidOptionRejectsWithoutExhaustionOrBuff()
        {
            var s=Create(green:false);var p=s.Player;var u=p.Units[0];
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(u,2));
            Assert.That(u.IsReady,Is.True);Assert.That(s.Context.UnitsResistAll,Is.False);
            p.Mana.AddCrystal(ManaColor.Green,1);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(u,2,1));
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));Assert.That(u.IsReady,Is.True);
            Assert.That(s.Context.UnitsResistAll,Is.False);
        }

        [Test] public void BuffCanBeUsedOutsideCombatAndExpiresEvenWhenRefillSkipped()
        {
            var s=Create();var p=s.Player;while(s.Phase!=Phase.End)s.Advance();
            s.UseUnit(p.Units[0],2);Assert.That(s.Context.UnitsResistAll,Is.True);
            s.Context.SkipDraw=true;new PlayerTurnEngine().EndTurn(p,DayPart.Day,null,s.Context);
            Assert.That(s.Context.UnitsResistAll,Is.False);
        }
    }
}
