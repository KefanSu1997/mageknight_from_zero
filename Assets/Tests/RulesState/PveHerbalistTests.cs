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
    public sealed class PveHerbalistTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string companion = "army_2_014", int green = 1, string enemy = "monster_032")
        {
            var p = new PlayerState { Fame = 8 };
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_2_002")));
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, companion)));
            p.Mana.AddCrystal(ManaColor.Red, 1); p.Mana.AddCrystal(ManaColor.Blue, 1);
            if (green > 0) p.Mana.AddCrystal(ManaColor.Green, green);
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice = "strong") => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void End(PveBattleSession s)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True);
        }
        private void TakeHeroDamageAndWin(PveBattleSession s)
        {
            s.Advance(); s.Advance(); s.Advance();
            Assert.That(s.Player.Wounds, Is.EqualTo(2));
            Play(s,21); s.Attack(0); End(s);
        }
        [Test] public void FreeGreenTokenPaysActualSavageSiegeWithoutCrystal()
        {
            var s=Create("army_2_008",0,"monster_017");var p=s.Player;var h=p.Units[0];
            Assert.That(h.Card.Level,Is.EqualTo(1));Assert.That(h.Card.Armor,Is.EqualTo(2));
            Assert.That(h.Card.RecruitLocationMask,Is.EqualTo(RecruitLocation.Village | RecruitLocation.Monastery));
            s.UseUnit(h,2);Assert.That(p.Mana.Tokens[ManaColor.Green],Is.EqualTo(1));
            Assert.That(p.Mana.Crystals.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(h,2));
            s.UseUnit(p.Units[1],1);Assert.That(p.Mana.Tokens.GetValueOrDefault(ManaColor.Green),Is.Zero);
            Assert.That(s.Context.SiegePool,Is.EqualTo(4));Assert.That(s.Attack(0).Kills,Is.EqualTo(1));End(s);
            Assert.That(p.Fame,Is.EqualTo(11));Assert.That(p.Units.All(u=>!u.IsReady),Is.True);
        }
        [Test] public void HealingSpendsGreenOnceAndRemovesTwoActualHandWounds()
        {
            var s=Create();TakeHeroDamageAndWin(s);var p=s.Player;
            s.UseUnit(p.Units[0],0);Assert.That(s.Context.Healing.Total,Is.EqualTo(2));
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.Zero);Assert.That(p.Wounds,Is.EqualTo(2));
            s.HealWound();s.HealWound();Assert.That(p.Wounds,Is.Zero);
            Assert.That(p.Deck.Hand.Count(c=>c.Type==CardType.Wound),Is.Zero);Assert.That(s.Context.Healing.Total,Is.Zero);
            Assert.That(p.Units[0].IsReady,Is.False);Assert.That(p.Fame,Is.EqualTo(10));
        }
        [Test] public void TwoHealingPaysOneLevelTwoWoundWithoutReadying()
        {
            var s=Create();var p=s.Player;var u=p.Units[1];s.Advance();Play(s,16);s.Block(0);s.Advance();s.Advance();
            s.UseUnit(u,1);Assert.That(u.Wounds,Is.EqualTo(1));s.Attack(0);End(s);
            s.UseUnit(p.Units[0],0);s.HealWound(1);
            Assert.That(s.Context.Healing.Total,Is.Zero);Assert.That(u.Wounds,Is.Zero);Assert.That(u.IsReady,Is.False);
            Assert.That(u.CanActivate,Is.False);Assert.That(p.Wounds,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ReadyRestoresRealExhaustedUnitButNeverHeals(bool wounded)
        {
            var s=Create();var p=s.Player;var u=p.Units[1];s.Advance();s.UseUnit(u,wounded?1:0,1);
            if(!wounded)s.PlaySideways(p.Deck.Hand.Single(c=>c.Id=="basic_card_000"));
            s.Block(0);s.Advance();s.Advance();Play(s,21);s.Attack(0);End(s);
            s.UseUnit(p.Units[0],1,target:1);
            Assert.That(u.IsReady,Is.True);Assert.That(u.Wounds,Is.EqualTo(wounded?1:0));
            Assert.That(u.CanActivate,Is.EqualTo(!wounded));Assert.That(p.Units[0].IsReady,Is.False);
            Assert.That(p.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
            // The same real player can carry this state into another encounter; no camp reset.
            var next=new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"),CardJsonLoader.LoadBasicActions());
            next.Advance();
            if(wounded)Assert.Throws<InvalidOperationException>(()=>next.UseUnit(u,0,1));
            else {next.UseUnit(u,0,1);Assert.That(next.Context.BlockPool,Is.EqualTo(3));}
        }
        [TestCase(0)] [TestCase(1)] public void SupportCannotBeActivatedDuringCombat(int ability)
        {
            var s=Create();s.Player.Units[1].Exhaust();
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(s.Player.Units[0],ability,target:1));
            Assert.That(s.Player.Units[0].IsReady,Is.True);Assert.That(s.Player.Mana.Crystals[ManaColor.Green],Is.EqualTo(1));
            Assert.That(s.Context.Healing.Total,Is.Zero);Assert.That(s.Player.Units[1].IsReady,Is.False);
            Assert.Throws<InvalidOperationException>(()=>CardUnitActions.Use(s.Player,s.Context,s.Player.Units[0],ability,unitTarget:s.Player.Units[1]));
        }
        [Test] public void MissingGreenRejectsBeforeExhaustionOrHealing()
        {
            var s=Create(green:0);TakeHeroDamageAndWin(s);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(s.Player.Units[0],0));
            Assert.That(s.Player.Units[0].IsReady,Is.True);Assert.That(s.Player.Wounds,Is.EqualTo(2));Assert.That(s.Context.Healing.Total,Is.Zero);
        }
        [TestCase("army_2_014",false)] [TestCase("army_003",true)]
        public void AlreadyReadyOrHigherLevelTargetIsRejectedAtomically(string companion,bool exhaust)
        {
            var s=Create(companion);TakeHeroDamageAndWin(s);if(exhaust)s.Player.Units[1].Exhaust();
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(s.Player.Units[0],1,target:1));
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(s.Player.Units[0],1,target:9));
            Assert.That(s.Player.Units[0].IsReady,Is.True);Assert.That(s.Player.Units[1].IsReady,Is.EqualTo(!exhaust));
        }
        [Test] public void InvalidTreatmentTargetAndInsufficientPointsDoNotConsume()
        {
            var s=Create();TakeHeroDamageAndWin(s);s.UseUnit(s.Player.Units[0],0);
            Assert.Throws<InvalidOperationException>(()=>s.HealWound(8));
            Assert.Throws<InvalidOperationException>(()=>s.HealWound(1));
            Assert.That(s.Context.Healing.Total,Is.EqualTo(2));
            s.HealWound();s.Player.Units[1].AddWounds(1);
            Assert.Throws<InvalidOperationException>(()=>s.HealWound(1));Assert.That(s.Context.Healing.Total,Is.EqualTo(1));
            Assert.That(s.Player.Units[1].Wounds,Is.EqualTo(1));
        }
    }
}
