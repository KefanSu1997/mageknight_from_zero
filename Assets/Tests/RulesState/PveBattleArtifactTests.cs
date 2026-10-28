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
    public sealed class PveBattleArtifactTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(string artifact, params string[] enemies)
        {
            var p = new PlayerState(0, "神器战斗快照");
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, "army_2_001")));
            foreach (var id in new[] { "basic_card_016", "basic_card_021", "basic_card_000" }) p.Deck.Hand.Add(new DeedCard(id, CardType.Action));
            p.Deck.Hand.Add(new DeedCard(artifact, CardType.Artifact)); p.Mana.AddCrystal(ManaColor.Blue);
            var catalog = CardJsonLoader.LoadMonsters();
            return new PveBattleSession(p, enemies.Select(id => catalog.Single(e => e.Id == id)), CardJsonLoader.LoadBasicActions(), artifacts: CardJsonLoader.LoadItems());
        }
        private DeedCard Artifact(PveBattleSession s) => s.Player.Deck.Hand.Single(c => c.Type == CardType.Artifact);

        [TestCase(false)] [TestCase(true)] public void HonorNormalAndBurnUseActualZonesAndGiveUnitArmorAndActionReward(bool burn)
        {
            var s = Create("items_000", "monster_032", "monster_032"); var card = Artifact(s); var unit = s.Player.Units.Single();
            s.PlayArtifact(card, burn, burn ? null : unit);
            Assert.That(unit.Banner, burn ? Is.Null : Is.SameAs(card));
            Assert.That(s.Player.Deck.RemovedCards.Contains(card), Is.EqualTo(burn));
            Assert.That(s.Player.Deck.PlayedCards.Count, Is.Zero); Assert.That(s.Player.Deck.Hand.Contains(card), Is.False);
            s.Advance(); s.UseUnit(unit, 2);
            Assert.That(s.Context.BlockPool, Is.EqualTo(5)); Assert.That(s.Player.Fame, Is.EqualTo(1));
            s.Block(0); s.Advance(); s.PlanDamage(1, 0, unit); s.Advance();
            Assert.That(unit.Wounds, Is.Zero, "原护甲3加旌旗1，以物抗完整吸收4"); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => s.PlayArtifact(card, burn, unit));
            Assert.That(s.Player.Fame, Is.EqualTo(1)); Assert.That(BannerActions.HonorBonus(s.Context, unit), Is.EqualTo(1));
        }

        [Test] public void SwordDiscardsExactSelectedInstanceAndAddsRealKillReward()
        {
            var s = Create("items_008", "monster_032"); var sword = Artifact(s); var material = s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_000");
            s.Advance(); s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_016"),"strong"); s.Block(0); s.Advance(); s.Advance();
            s.PlayArtifact(sword, false, materials: new[]{material});
            Assert.That(s.Player.Deck.DiscardPile.Single(), Is.SameAs(material));
            Assert.That(s.Player.Deck.PlayedCards.Last(), Is.SameAs(sword)); Assert.That(s.Context.MeleePool, Is.EqualTo(3));
            var attack=s.Attack(0); Assert.That(attack.Kills,Is.EqualTo(1)); Assert.That(attack.Fame,Is.EqualTo(3)); s.Advance(); Assert.That(s.Won,Is.True);
        }

        [Test] public void BurnedSwordDoublesRealPhysicalContributionIgnoresResistanceAndCannotReappear()
        {
            var s=Create("items_008","monster_006"); var sword=Artifact(s);
            s.Advance(); s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_016"),"strong");s.Block(0);s.Advance();s.Advance();
            s.PlayArtifact(sword,true);s.Play(s.Player.Deck.Hand.Single(c=>c.Id=="basic_card_021"),"attack");
            var attack=s.Attack(0);Assert.That(attack.EffectivePower,Is.EqualTo(4));Assert.That(attack.Kills,Is.EqualTo(1));Assert.That(attack.Fame,Is.EqualTo(5));
            s.Advance();Assert.That(s.Won,Is.True);Assert.That(s.Player.Deck.RemovedCards.Single(),Is.SameAs(sword));
            Assert.That(s.Player.Deck.Hand.Concat(s.Player.Deck.PlayedCards).Concat(s.Player.Deck.DiscardPile).Contains(sword),Is.False);
        }

        [Test] public void InvalidMaterialAndMissingUnitKeepZonesAndInputContextUnchanged()
        {
            var s=Create("items_008","monster_032");var sword=Artifact(s);var material=s.Player.Deck.Hand.First();
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(sword,false,materials:new[]{material}));
            s.Advance();s.Advance();s.Advance();
            var previousUnit=s.Player.Units.Single();s.Context.TargetUnit=previousUnit;s.Context.CardsToDiscard.Add(material);
            Assert.Throws<InvalidOperationException>(()=>s.PlayArtifact(sword,false,materials:new[]{material,material}));
            Assert.That(s.Player.Deck.Hand.Contains(material),Is.True);Assert.That(s.Player.Deck.Hand.Contains(sword),Is.True);
            Assert.That(s.Context.CardsToDiscard.Single(),Is.SameAs(material));Assert.That(s.Context.TargetUnit,Is.SameAs(previousUnit));
            Assert.That(s.Context.MeleePool,Is.Zero);
            var banner=Create("items_000","monster_032"); var actual=Artifact(banner);
            Assert.Throws<InvalidOperationException>(()=>banner.PlayArtifact(actual,false));
            Assert.That(banner.Player.Deck.Hand.Contains(actual),Is.True);Assert.That(banner.Player.Units.Single().Banner,Is.Null);
        }
    }
}
