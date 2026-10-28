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
    public sealed class PveCourageBannerTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create()
        {
            var p = new PlayerState { Fame = 8 };
            for (int i=0;i<2;i++) p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_014")));
            p.Mana.AddCrystal(ManaColor.Blue,2);
            foreach (int n in new[]{16,21,0,11}) p.Deck.Hand.Add(new DeedCard("basic_card_"+n.ToString("000"),CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_003",CardType.Artifact));
            return new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(e=>e.Id=="monster_032"),CardJsonLoader.LoadBasicActions(),artifacts:CardJsonLoader.LoadItems());
        }
        private DeedCard Banner(PveBattleSession s)=>s.Player.Deck.Hand.Single(c=>c.Id=="items_003");
        private void Fight(PveBattleSession s,bool wound)
        {
            s.Advance();s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_016"),"strong");s.Block(0);s.Advance();s.Advance();
            foreach(var u in s.Player.Units)s.UseUnit(u,wound?1:0);
            Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(wound?12:6));s.Advance();
            Assert.That(s.Won,Is.True);Assert.That(s.Player.Fame,Is.EqualTo(10));
        }
        [TestCase(false)] [TestCase(true)]
        public void NormalBannerReadiesOnlyAssignedActualUnitOnceOutsideCombat(bool wound)
        {
            var s=Create();var b=Banner(s);var u=s.Player.Units[0];s.PlayArtifact(b,false,u);
            Assert.Throws<InvalidOperationException>(()=>s.UseCourage(u));Assert.That(u.CourageBannerSpent,Is.False);
            Fight(s,wound);s.UseCourage(u);
            Assert.That(u.IsReady,Is.True);Assert.That(s.Player.Units[1].IsReady,Is.False);
            Assert.That(s.Player.Units.All(x=>x.Wounds==(wound?1:0)),Is.True);Assert.That(u.CanActivate,Is.EqualTo(!wound));
            Assert.That(u.Banner,Is.SameAs(b));Assert.That(u.CourageBannerSpent,Is.True);
            Assert.Throws<InvalidOperationException>(()=>s.UseCourage(u));Assert.That(u.IsReady,Is.True);
            Assert.That(s.Context.HealedUnitsThisTurn,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void BurnReadiesBothSurvivingUnitsAndPreservesTheirWounds(bool wound)
        {
            var s=Create();var b=Banner(s);Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(b,true));
            Assert.That(s.Player.Deck.Hand.Contains(b),Is.True);Fight(s,wound);s.PlayArtifact(b,true);
            Assert.That(s.Player.Units.All(u=>u.IsReady&&u.Wounds==(wound?1:0)),Is.True);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(b));Assert.That(s.Context.HealedUnitsThisTurn,Is.Empty);
            Assert.That(s.Player.Wounds,Is.Zero);
        }
        [Test] public void MissingBannerAndForeignUnitCannotUsePostcombatReady()
        {
            var s=Create();Fight(s,false);var u=s.Player.Units[0];
            Assert.Throws<InvalidOperationException>(()=>s.UseCourage(u));s.PlayArtifact(Banner(s),false,u);
            Assert.Throws<InvalidOperationException>(()=>s.UseCourage(new UnitState(u.Card)));
            Assert.That(u.IsReady,Is.False);Assert.That(u.CourageBannerSpent,Is.False);
        }
    }
}
