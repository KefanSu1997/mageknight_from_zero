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
    public sealed class PveCommonerTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(params string[] units)
        {
            var p = new PlayerState { Fame = units.Length > 1 ? 8 : 3 };
            foreach(var id in units) p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder,id)));
            p.Mana.AddCrystal(ManaColor.Red,1);p.Mana.AddCrystal(ManaColor.Blue,1);
            foreach(int n in new[]{16,21,0,7})p.Deck.Hand.Add(new DeedCard("basic_card_"+n.ToString("000"),CardType.Action));
            return new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"),CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s,int n,string mode="strong")=>s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_"+n.ToString("000")),mode);
        private void Finish(PveBattleSession s)
        {
            s.Advance();Assert.That(s.Won,Is.True);Assert.That(s.Player.Wounds,Is.Zero);
            Assert.That(s.Player.Fame,Is.EqualTo(s.Player.Units.Count>1?10:5));
        }
        [TestCase("army_2_006",0,0)] [TestCase("army_2_011",1,-1)]
        public void OriginalAttackExhaustsOnceAndPaysPrintedReputation(string id,int ability,int reputation)
        {
            var s=Create(id);var u=s.Player.Units[0];s.Advance();Play(s,16);s.Block(0);s.Advance();s.Advance();
            s.UseUnit(u,ability);Assert.That(s.Player.Reputation,Is.EqualTo(reputation));Assert.That(u.IsReady,Is.False);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(u,ability));Assert.That(s.Player.Reputation,Is.EqualTo(reputation));
            if(id=="army_2_006")s.PlaySideways(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_000"));
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));Finish(s);
        }
        [TestCase("army_2_006")] [TestCase("army_2_011")]
        public void OriginalBlockDoesNotLoseReputationAndActuallyPreventsWounds(string id)
        {
            var s=Create(id);s.Advance();s.UseUnit(s.Player.Units[0],0,id=="army_2_006"?1:0);
            if(id=="army_2_006")Play(s,16,"block");else s.PlaySideways(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_000"));
            Assert.That(s.Context.BlockPool,Is.EqualTo(4));s.Block(0);s.Advance();s.Advance();
            Assert.That(s.Player.Reputation,Is.Zero);Play(s,21);s.Attack(0);Finish(s);
        }
        [Test] public void PeasantInfluencePaysThugDamageAfterAtomicInsufficientFundsRejection()
        {
            var s=Create("army_2_006","army_2_011");var p=s.Player;s.Advance();s.Advance();
            s.PlanDamage(0,0,p.Units[1]);Assert.Throws<InvalidOperationException>(()=>s.Advance());
            Assert.That(s.Phase,Is.EqualTo(Phase.AssignDamage));Assert.That(p.Wounds,Is.Zero);Assert.That(p.Units[1].Wounds,Is.Zero);
            s.UseUnit(p.Units[0],1);Assert.That(s.Context.InfluencePool,Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(p.Units[0],1));Assert.That(s.Context.InfluencePool,Is.EqualTo(2));
            s.Advance();Assert.That(s.Context.InfluencePool,Is.Zero);Assert.That(p.Units[1].Wounds,Is.EqualTo(1));
            Assert.That(p.Units[1].IsReady,Is.True);Assert.That(p.Units[1].CanActivate,Is.False);Assert.That(p.Reputation,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(p.Units[1],1));Assert.That(p.Reputation,Is.Zero);
            Play(s,21);s.Attack(0);Finish(s);
        }
        [Test] public void ThugInfluenceCanPayItsOwnDamageWhileExhausted()
        {
            var s=Create("army_2_011");var p=s.Player;s.Advance();s.Advance();s.UseUnit(p.Units[0],2);
            Assert.That(s.Context.InfluencePool,Is.EqualTo(4));Assert.That(p.Reputation,Is.EqualTo(-1));
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(p.Units[0],2));Assert.That(p.Reputation,Is.EqualTo(-1));
            s.PlanDamage(0,0,p.Units[0]);Assert.That(s.Context.InfluencePool,Is.EqualTo(4));
            s.Advance();Assert.That(s.Context.InfluencePool,Is.EqualTo(2));Assert.That(p.Units[0].Wounds,Is.EqualTo(1));
            Assert.That(p.Units[0].IsReady,Is.False);Play(s,21);s.Attack(0);Finish(s);
        }
        [TestCase("army_2_006",0)] [TestCase("army_2_006",2)] [TestCase("army_2_011",1)]
        public void IllegalBattlePhaseDoesNotExhaustOrAlterResources(string id,int ability)
        {
            var s=Create(id);Assert.Throws<InvalidOperationException>(()=>s.UseUnit(s.Player.Units[0],ability));
            Assert.That(s.Player.Units[0].IsReady,Is.True);Assert.That(s.Player.Reputation,Is.Zero);
            Assert.That(s.Context.MovementPool,Is.Zero);Assert.That(s.Context.InfluencePool,Is.Zero);
        }
    }
}
