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
    public sealed class PveGoldenGrailTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(bool herbs = false)
        {
            var p = new PlayerState { Fame = herbs ? 8 : 0 };
            if (herbs) p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder,"army_2_002")));
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder,"army_2_014")));
            p.Mana.AddCrystal(ManaColor.Red,1);p.Mana.AddCrystal(ManaColor.Blue,1);p.Mana.AddCrystal(ManaColor.Green,1);
            foreach(int n in new[]{16,21,0,11}) p.Deck.Hand.Add(new DeedCard("basic_card_"+n.ToString("000"),CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_012",CardType.Artifact));
            p.Deck.PutOnTop(new DeedCard("basic_card_011",CardType.Action));p.Deck.PutOnTop(new DeedCard("basic_card_000",CardType.Action));
            return new PveBattleSession(p,CardJsonLoader.LoadMonsters().Where(m=>m.Id=="monster_032"),CardJsonLoader.LoadBasicActions(),artifacts:CardJsonLoader.LoadItems());
        }
        private void Play(PveBattleSession s,int n) => s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_"+n.ToString("000")),"strong");
        private void Win(PveBattleSession s,bool unitWound=false,bool heroWounds=false)
        {
            s.Advance();if(!heroWounds){Play(s,16);s.Block(0);}
            s.Advance();s.Advance();
            if(unitWound)s.UseUnit(s.Player.Units.Last(),1);else Play(s,21);
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));s.Advance();Assert.That(s.Won,Is.True);
        }
        private DeedCard Grail(PveBattleSession s,bool burn)
        {
            var card=s.Player.Deck.Hand.Single(c=>c.Id=="items_012");s.PlayArtifact(card,burn);return card;
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualHeroWoundsRewardOnlySpentBasePointsOrDrawRealCards(bool burn)
        {
            var s=Create();Win(s,heroWounds:true);var p=s.Player;
            Assert.That(p.Wounds,Is.EqualTo(2));var card=Grail(s,burn);
            Assert.That(p.Fame,Is.EqualTo(2));Assert.That(s.Context.Healing.Total,Is.EqualTo(burn?6:2));
            s.HealWound();Assert.That(p.Fame,Is.EqualTo(burn?2:3));Assert.That(p.Deck.DrawPileCount,Is.EqualTo(burn?1:2));
            s.HealWound();Assert.That(p.Fame,Is.EqualTo(burn?2:4));Assert.That(p.Wounds,Is.Zero);
            Assert.That(p.Deck.DrawPileCount,Is.EqualTo(burn?0:2));Assert.That(s.Context.Healing.Total,Is.EqualTo(burn?4:0));
            Assert.That(p.Deck.Hand.Count(c=>c.Id=="basic_card_000"),Is.EqualTo(burn?2:1));
            Assert.That(p.Deck.Hand.Count(c=>c.Type==CardType.Wound),Is.Zero);
            Assert.That((burn?p.Deck.RemovedCards:p.Deck.PlayedCards).Contains(card),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void LevelTwoUnitCostsTwoPointsNeverDrawsOrReadies(bool burn)
        {
            var s=Create();Win(s,unitWound:true);var p=s.Player;var u=p.Units[0];Grail(s,burn);s.HealWound(0);
            Assert.That(u.Wounds,Is.Zero);Assert.That(u.IsReady,Is.False);Assert.That(p.Deck.DrawPileCount,Is.EqualTo(2));
            Assert.That(s.Context.Healing.Total,Is.EqualTo(burn?4:0));Assert.That(p.Fame,Is.EqualTo(burn?2:4));
        }
        [TestCase(false)] [TestCase(true)]
        public void MixedHerbalistAndGrailHealingPreservesSourceFame(bool grailFirst)
        {
            var s=Create(true);Win(s,unitWound:true,heroWounds:true);var p=s.Player;
            Grail(s,false);s.UseUnit(p.Units[0],0);Assert.That(s.Context.Healing.Total,Is.EqualTo(4));
            s.HealWound(grailFirst:grailFirst);s.HealWound(grailFirst:grailFirst);
            Assert.That(p.Fame,Is.EqualTo(grailFirst?12:10));Assert.That(s.Context.Healing.Grail,Is.EqualTo(grailFirst?0:2));
            s.HealWound(1,grailFirst);Assert.That(p.Fame,Is.EqualTo(12));Assert.That(p.Units[1].Wounds,Is.Zero);
            Assert.That(p.Units[1].IsReady,Is.False);Assert.That(s.Context.Healing.Total,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void CombatAndRepeatedPlayRejectWithoutMovingTheCard(bool burn)
        {
            var s=Create();var p=s.Player;var card=p.Deck.Hand.Single(c=>c.Id=="items_012");
            Assert.That(s.CanUseArtifact(card),Is.False);Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(card,burn));
            Assert.That(p.Deck.Hand.Contains(card),Is.True);Assert.That(p.Deck.RemovedCards,Is.Empty);Assert.That(s.Context.Healing.Total,Is.Zero);
            Win(s,heroWounds:true);s.PlayArtifact(card,burn);Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(card,burn));
            Assert.That(s.Context.Healing.Total,Is.EqualTo(burn?6:2));Assert.That(p.Fame,Is.EqualTo(2));
        }
    }
}
