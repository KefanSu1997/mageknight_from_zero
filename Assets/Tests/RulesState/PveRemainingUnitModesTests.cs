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
    public sealed class PveRemainingUnitModesTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(bool golem, string enemy = "monster_032")
        {
            var p = new PlayerState(0, "原部队能力补验");
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, golem ? "army_2_001" : "army_003")));
            p.Mana.AddCrystal(ManaColor.Red, golem ? 2 : 1); p.Mana.AddCrystal(ManaColor.Blue);
            foreach (int n in golem ? new[] {16,21,0,11} : new[] {16,21,0})
                p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice) => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_"+n.ToString("000")), choice);
        private void Sideways(PveBattleSession s) => s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));

        [TestCase(true,false)] [TestCase(true,true)] [TestCase(false,false)] [TestCase(false,true)]
        public void MissingOrdinaryAttackAndBlockModesFinishRealBattle(bool golem, bool block)
        {
            var s=Create(golem); var unit=s.Player.Units.Single(); s.Advance();
            if (block) { s.UseUnit(unit,0,1); if(golem) Play(s,16,"block"); }
            else Play(s,16,"strong");
            Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(golem&&block ? 4 : 5));
            s.Block(0); s.Advance(); s.Advance();
            if(block) Play(s,21,"strong");
            else { s.UseUnit(unit,0); if(golem) Sideways(s); }
            Assert.That(s.Context.MeleePool,Is.EqualTo(block ? 4 : golem ? 3 : 5));
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(unit,0));
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));s.Advance();
            Assert.That(s.Won,Is.True);Assert.That(s.Player.Fame,Is.EqualTo(2));
            Assert.That(s.Player.Wounds,Is.Zero);Assert.That(unit.Wounds,Is.Zero);Assert.That(unit.IsReady,Is.False);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo((golem ? 2 : 1)-(block ? 1 : 0)));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(block ? 1 : 0));
        }

        [Test] public void GolemFireBlockPaysRedAndStopsIceThenRealCardsKillResistantEnemy()
        {
            var s=Create(true,"monster_004");var unit=s.Player.Units.Single();s.Advance();s.UseUnit(unit,1);
            Assert.That(s.Context.CombatPower.Blocks.Single().Element,Is.EqualTo(Element.Fire));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.EqualTo(1));
            Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(4));Assert.That(s.BlockPreview(0).Required,Is.EqualTo(2));
            s.Block(0);s.Advance();s.Advance();Play(s,21,"strong");Play(s,11,"strong");Sideways(s);
            var result=s.Attack(0);Assert.That(result.EffectivePower,Is.EqualTo(4));Assert.That(result.Kills,Is.EqualTo(1));s.Advance();
            Assert.That(s.Won,Is.True);Assert.That(s.Player.Fame,Is.EqualTo(6));Assert.That(s.Player.Wounds,Is.Zero);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red],Is.Zero);Assert.That(s.Player.Mana.Crystals[ManaColor.Blue],Is.Zero);
            Assert.That(unit.IsReady,Is.False);Assert.That(unit.Wounds,Is.Zero);
        }

        [Test] public void SameElementFireBlockIsHalvedAndInsufficientBlockDoesNotReserve()
        {
            var s=Create(true,"monster_029");s.Advance();s.UseUnit(s.Player.Units.Single(),1);
            Assert.That(s.BlockPreview(0).Effective,Is.EqualTo(2));Assert.That(s.BlockPreview(0).Required,Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(()=>s.Block(0));Assert.That(s.ReservedBlocks,Is.Empty);
            Assert.That(s.Context.BlockPool,Is.EqualTo(4));Assert.That(s.IsBlocked(0),Is.False);
        }

        [Test] public void GolemFireBlockCannotSpendBlueInsteadOfRed()
        {
            var s=Create(true);s.Player.Mana.Crystals[ManaColor.Red]=0;s.Advance();var unit=s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(()=>s.UseUnit(unit,1));
            Assert.That(unit.IsReady,Is.True);Assert.That(s.Context.BlockPool,Is.Zero);
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue],Is.EqualTo(1));
        }
    }
}
