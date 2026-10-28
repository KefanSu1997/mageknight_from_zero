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
    public sealed class PveFortitudeBannerTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(string enemy = "monster_032")
        {
            var p = new PlayerState { Fame = 8 };
            for (int i=0;i<2;i++) p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_014")));
            Assert.That(p.FreeSlots, Is.Zero);
            p.Mana.AddCrystal(ManaColor.Red,2);p.Mana.AddCrystal(ManaColor.Blue,2);
            foreach (int n in new[] {16,21,0,11}) p.Deck.Hand.Add(new DeedCard("basic_card_"+n.ToString("000"),CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_018",CardType.Artifact));
            return new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(e=>e.Id==enemy),CardJsonLoader.LoadBasicActions(),artifacts:CardJsonLoader.LoadItems());
        }
        private DeedCard Banner(PveBattleSession s)=>s.Player.Deck.Hand.Single(c=>c.Id=="items_018");
        private void Play(PveBattleSession s,int id,string choice)=>s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_"+id.ToString("000")),choice);
        private void Melee(PveBattleSession s) { s.Advance();Play(s,16,"strong");s.Block(0);s.Advance();s.Advance(); }
        [TestCase(false)] [TestCase(true)]
        public void SelectedBannerPreventsSelfWoundAndCanBeCancelled(bool cancel)
        {
            var s=Create();var u=s.Player.Units[0];var b=Banner(s);s.PlayArtifact(b,false,u);Melee(s);
            s.SelectFortitude(u,true);if(cancel)s.SelectFortitude(u,false);s.UseUnit(u,1);
            Assert.That(u.Wounds,Is.EqualTo(cancel?1:0));Assert.That(u.FortitudeBannerSpent,Is.EqualTo(!cancel));
            Assert.That(s.Player.Deck.Hand.Contains(b),Is.False);Assert.That(u.Banner,Is.SameAs(b));
            Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(6));s.Advance();Assert.That(s.Won,Is.True);
            Assert.That(u.IsReady,Is.False);Assert.That(s.Player.Fame,Is.EqualTo(10));
            Assert.That(u.FortitudeProtectionSelected,Is.False);
        }
        [Test] public void SelectedBannerPreventsActualPoisonDamageThenUnitStillActivates()
        {
            var s=Create("monster_006");var u=s.Player.Units[0];s.PlayArtifact(Banner(s),false,u);
            s.Advance();s.Advance();s.SelectFortitude(u,true);s.PlanDamage(0,0,u);s.Advance();
            Assert.That(u.Wounds,Is.Zero);Assert.That(u.IsReady,Is.True);Assert.That(u.FortitudeBannerSpent,Is.True);
            Assert.That(s.Player.Wounds,Is.Zero);Assert.Throws<InvalidOperationException>(()=>s.SelectFortitude(u,true));
            s.UseUnit(u,0);Play(s,21,"strong");Assert.That(s.Attack(0).EffectivePower,Is.EqualTo(3));
            s.Advance();Assert.That(s.Won,Is.True);Assert.That(s.Player.Fame,Is.EqualTo(12));
        }
        [Test] public void BurnOnlyAfterBattleHealsBothActuallyWoundedUnitsWithoutReadyingThem()
        {
            var s=Create();var b=Banner(s);
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(b,true));Assert.That(s.Player.Deck.Hand.Contains(b),Is.True);
            Melee(s);s.UseUnit(s.Player.Units[0],1);s.UseUnit(s.Player.Units[1],1);
            Assert.That(s.Player.Units.All(u=>u.Wounds==1&&!u.IsReady),Is.True);
            s.Attack(0);s.Advance();s.PlayArtifact(b,true);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(b));
            Assert.That(s.Player.Units.All(u=>u.Wounds==0&&!u.IsReady),Is.True);
            Assert.That(s.Context.HealedUnitsThisTurn.Count,Is.EqualTo(2));
            Assert.That(s.Player.Wounds,Is.Zero);Assert.That(s.Player.Fame,Is.EqualTo(10));
        }
        [Test] public void MissingAndForeignBannerTargetsCannotSpendTheArtifact()
        {
            var s=Create();var b=Banner(s);
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(b,false));
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(b,false,new UnitState(s.Player.Units[0].Card)));
            Assert.That(s.Player.Deck.Hand.Contains(b),Is.True);Assert.That(s.Player.Deck.RemovedCards,Is.Empty);
        }
    }
}
