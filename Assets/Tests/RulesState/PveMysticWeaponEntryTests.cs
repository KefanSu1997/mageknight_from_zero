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
    public sealed class PveMysticWeaponEntryTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private static (PveBattleSession s, DeedCard box, ArtifactCard original) Create(string id, string enemy = "monster_032", int count = 1)
        {
            var p = new PlayerState();
            var box = new DeedCard("items_024", CardType.Artifact); p.Deck.Hand.Add(box);
            var original = new ArtifactCard(id);
            var c = new ActionContext { ArtifactSupply = new ArtifactSupply(), EffectRandom = new Random(2) };
            c.ArtifactSupply.SetDeck(new[] { original, new ArtifactCard("items_004") });
            var s = new PveBattleSession(p, Enumerable.Range(0,count).Select(_ => CardJsonLoader.LoadMonsters().Single(m => m.Id == enemy) with { }),
                CardJsonLoader.LoadBasicActions(), c, CardJsonLoader.LoadItems());
            CardPlayActions.PlayArtifact(p,c,box,CardJsonLoader.LoadItems().Single(x=>x.Id==box.Id));
            return (s,box,original);
        }
        private static void EndAndCheckSupply(PveBattleSession s, ArtifactCard original)
        {
            s.Context.SkipDraw=true;
            new PlayerTurnEngine().EndTurn(s.Player,DayPart.Day,null,s.Context);
            Assert.That(s.Context.ArtifactSupply.Draw().Id,Is.EqualTo("items_004"));
            Assert.That(s.Context.ArtifactSupply.Draw(),Is.SameAs(original));
        }
        [TestCase("items_008",3)] [TestCase("items_016",2)]
        public void ActualMaterialAndPhysicalBoxProduceOriginalAttack(string id,int power)
        {
            var (s,box,original)=Create(id); var p=s.Player;
            if(id=="items_008") { s.Advance();s.Advance();s.Advance(); }
            var material=new DeedCard("basic_card_000",CardType.Action);p.Deck.Hand.Add(material);
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(box,false,null,box));
            Assert.That(s.Context.MysticBox.Used,Is.False);Assert.That(p.Deck.Hand,Does.Contain(material));
            s.PlayArtifact(box,false,null,material);
            Assert.That(p.Deck.DiscardPile,Does.Contain(material));Assert.That(p.Deck.PlayedCards,Does.Contain(box));
            Assert.That(s.Context.CardsToDiscard,Is.Empty);Assert.That(p.Fame,Is.EqualTo(1));
            Assert.That(s.Context.CombatPower.Attacks.Single().Value,Is.EqualTo(power));
            Assert.That(s.Context.CombatPower.Attacks.Single().SourceCard,Is.SameAs(box));
            if(id=="items_008") Assert.That(s.Attack(0).Kills,Is.EqualTo(1));
            Assert.That(s.CanUseArtifact(box),Is.False);
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(box,false));
            EndAndCheckSupply(s,original);
        }
        [Test] public void BorrowedShieldRejectsPhaseAndForeignBoxThenConsumesTwoActualSlots()
        {
            var (s,box,original)=Create("items_020","monster_015");
            Assert.That(s.CanUseArtifact(box),Is.False);
            Assert.Throws<InvalidOperationException>(()=>s.PlayShield(box,true,true));s.Advance();
            Assert.That(s.CanUseArtifact(new DeedCard(box.Id,CardType.Artifact)),Is.False);
            s.PlayShield(box,true,true);
            s.Block(0,0);s.Block(0,1);Assert.That(s.Context.BlockPool,Is.EqualTo(4));
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(box));Assert.That(s.Player.Fame,Is.EqualTo(1));
            EndAndCheckSupply(s,original);
        }
        [Test] public void BorrowedHornUsesPrintedBonusDiceAndRealAttack()
        {
            var (s,box,original)=Create("items_009");
            Assert.Throws<InvalidOperationException>(()=>s.PlayHorn(box,true,6));
            Assert.That(s.Context.MysticBox.Used,Is.False);Assert.That(s.Context.LastManaRolls,Is.Empty);
            s.PlayHorn(box,true,3);
            Assert.That(s.Context.LastManaRolls,Is.EqualTo(new[]{ManaColor.Gold,ManaColor.Green,ManaColor.Red}));
            Assert.That(s.Context.SiegePool,Is.EqualTo(8));Assert.That(s.Player.Wounds,Is.EqualTo(1));
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(box));EndAndCheckSupply(s,original);
        }
        [Test] public void BorrowedFearValidatesTargetsBeforeBurnAndRestoresInputs()
        {
            var (s,box,original)=Create("items_001","monster_015",2);
            s.Context.SpellTargetIndices.Add(7);
            Assert.Throws<InvalidOperationException>(()=>s.BurnFearBanner(box,0,0));
            Assert.That(s.Context.SpellTargetIndices,Is.EqualTo(new[]{7}));
            Assert.That(s.Context.MysticBox.Used,Is.False);Assert.That(s.Player.Fame,Is.Zero);
            s.BurnFearBanner(box,0,1);Assert.That(s.Context.SkipAttackIndices,Is.EquivalentTo(new[]{0,1}));
            Assert.That(s.Context.SpellTargetIndices,Is.EqualTo(new[]{7}));
            s.Advance();s.Advance();s.Advance();Assert.That(s.Player.Wounds,Is.Zero);
            Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(box));EndAndCheckSupply(s,original);
        }
        [Test] public void BorrowedHarvesterSelectsActualKillCrystal()
        {
            var (s,box,original)=Create("items_019");s.Advance();s.Advance();s.Advance();
            s.PlayArtifact(box,false);s.ChooseKillCrystal(0,ManaColor.White);
            Assert.That(s.Attack(0).Kills,Is.EqualTo(1));Assert.That(s.Player.Mana.Crystals[ManaColor.White],Is.EqualTo(1));
            EndAndCheckSupply(s,original);
        }
        [Test] public void BorrowedResourceUsesExplicitColorThroughSameInspectorEntry()
        {
            var (s,box,original)=Create("items_015");s.PlayResourceArtifact(box,false,ManaColor.Blue);
            Assert.That(s.Player.Mana.Tokens[ManaColor.Blue],Is.EqualTo(1));
            Assert.That(s.CanUseArtifact(box),Is.False);EndAndCheckSupply(s,original);
        }
    }
}
