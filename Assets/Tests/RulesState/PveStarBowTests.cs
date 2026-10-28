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
    public sealed class PveStarBowTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(bool siege = false, params string[] enemies)
        {
            var p = new PlayerState { Fame = 3 };
            p.Units.Add(new UnitState(UnitJsonLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"), siege ? "army_004" : "army_003")));
            p.Mana.AddCrystal(ManaColor.Red, siege ? 1 : 2); p.Mana.AddCrystal(ManaColor.Blue, 1); p.Mana.AddCrystal(ManaColor.White, 1);
            foreach (int n in new[] { 16, 21, 0, 11, 7 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            p.Deck.Hand.Add(new DeedCard("items_016", CardType.Artifact));
            return new PveBattleSession(p, enemies.Select(id => CardJsonLoader.LoadMonsters().Single(m => m.Id == id)), CardJsonLoader.LoadBasicActions(), artifacts: CardJsonLoader.LoadItems());
        }
        private DeedCard Card(PveBattleSession s, string id) => s.Player.Deck.Hand.Single(c => c.Id == id);
        private void Play(PveBattleSession s, int n) => s.Play(Card(s, "basic_card_" + n.ToString("000")), "strong");
        private void Bow(PveBattleSession s, bool burn, params DeedCard[] materials) => s.PlayArtifact(Card(s, "items_016"), burn, materials: materials);
        private void End(PveBattleSession s) { while (s.Phase != Phase.End) s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Player.Wounds, Is.Zero); }

        [Test] public void NormalDiscardsRealMaterialsAndRewardsLaterAttackInSamePhase()
        {
            var s = Create(false, "monster_032", "monster_032");
            var materials = new[] { Card(s, "basic_card_016"), Card(s, "basic_card_000") };
            Bow(s, false, materials); Assert.That(s.Player.Deck.DiscardPile.Count, Is.EqualTo(2));
            foreach (var material in materials) Assert.That(s.Player.Deck.DiscardPile.Any(c => ReferenceEquals(c, material)), Is.True);
            Assert.That(s.Context.RangedPool, Is.EqualTo(4)); Assert.That(s.Player.Deck.PlayedCards.Single().Id, Is.EqualTo("items_016"));
            Assert.That(s.Attack(0).Fame, Is.EqualTo(3));
            s.UseUnit(s.Player.Units[0], 1); Assert.That(s.Attack(1).Fame, Is.EqualTo(3));
            End(s); Assert.That(s.Player.Fame, Is.EqualTo(9)); Assert.That(s.Context.BowFamePerKill, Is.Zero);
        }

        [Test] public void ZeroMaterialStillRewardsCurrentPhaseButNotLaterMeleeKill()
        {
            var s = Create(false, "monster_032", "monster_032"); Bow(s, false);
            Assert.That(s.Context.CombatPower.Attacks, Is.Empty); Assert.That(s.Player.Deck.DiscardPile, Is.Empty);
            s.UseUnit(s.Player.Units[0], 1); Assert.That(s.Attack(0).Fame, Is.EqualTo(3));
            s.Advance(); Assert.That(s.Context.BowFamePerKill, Is.Zero); Play(s, 16); s.Block(1); s.Advance(); s.Advance();
            Play(s, 21); Assert.That(s.Attack(1).Fame, Is.EqualTo(2)); End(s); Assert.That(s.Player.Fame, Is.EqualTo(8));
        }

        [Test] public void BurnDoublesOriginalFireRangedAndRemainsUntilRealTurnEnd()
        {
            var s = Create(false, "monster_004"); var artifact = Card(s, "items_016"); Bow(s, true);
            Assert.That(s.Player.Deck.RemovedCards.Single(), Is.SameAs(artifact));
            s.UseUnit(s.Player.Units[0], 1); Assert.That(s.AttackTotal(AttackType.Ranged), Is.EqualTo(12));
            Assert.That(s.Context.CombatPower.Attacks.Single().Element, Is.EqualTo(Element.Fire));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(12)); End(s); Assert.That(s.Player.Fame, Is.EqualTo(8));
            Assert.That(s.Context.StarBowActive, Is.True); s.Context.SkipDraw = true;
            new PlayerTurnEngine().EndTurn(s.Player, DayPart.Day, null, s.Context);
            Assert.That(s.Context.StarBowActive, Is.False);
            Assert.That(s.Player.Deck.RemovedCards.Single(), Is.SameAs(artifact));
        }

        [Test] public void EachOriginalRangedContributionMustChooseSiegeBeforeFortifiedKill()
        {
            var s = Create(false, "monster_017"); Bow(s, true); Play(s, 7); Play(s, 11);
            s.ChooseBowAttack(0, 2); Assert.That(s.AttackTotal(AttackType.Siege), Is.EqualTo(3));
            Assert.That(s.AttackTotal(AttackType.Ranged), Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            Assert.That(s.Context.CombatPower.Attacks.Count, Is.EqualTo(2));
            s.ChooseBowAttack(1, 2); Assert.That(s.AttackTotal(AttackType.Siege), Is.EqualTo(6));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(6)); End(s); Assert.That(s.Player.Fame, Is.EqualTo(7));
        }

        [Test] public void SiegeCanRemainOriginalButDoublingConvertsItToRangedAndLosesFortificationAccess()
        {
            var s = Create(true, "monster_017"); Bow(s, true); s.UseUnit(s.Player.Units[0], 1);
            Assert.That(s.AttackTotal(AttackType.Siege), Is.EqualTo(5));
            s.ChooseBowAttack(0, 1); Assert.That(s.AttackTotal(AttackType.Ranged), Is.EqualTo(10));
            Assert.That(s.AttackTotal(AttackType.Siege), Is.Zero); Assert.Throws<InvalidOperationException>(() => s.Attack(0));
            s.ChooseBowAttack(0, 0); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(5)); End(s);
            Assert.That(s.Player.Fame, Is.EqualTo(6));
        }

        [Test] public void DoubledSiegeKeepsIceElementAndUsesRealEnemyResistance()
        {
            var s = Create(true, "monster_029"); Bow(s, true); s.UseUnit(s.Player.Units[0], 2); s.ChooseBowAttack(0, 1);
            Assert.That(s.Context.CombatPower.Attacks.Single().Element, Is.EqualTo(Element.Ice));
            Assert.That(s.AttackTotal(AttackType.Ranged), Is.EqualTo(10));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(10)); End(s); Assert.That(s.Player.Fame, Is.EqualTo(8));
        }

        [Test] public void InvalidMaterialsChoicesAndForeignInstanceNeverConsumeCards()
        {
            var s = Create(false, "monster_032"); var real = Card(s, "basic_card_000");
            Assert.Throws<InvalidOperationException>(() => Bow(s, false, real, new DeedCard(real.Id, real.Type)));
            Assert.That(s.Player.Deck.Hand.Count, Is.EqualTo(6)); Assert.That(s.Player.Deck.DiscardPile, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => s.ChooseBowAttack(0, 1));
            Bow(s, true); Play(s, 7); var before = s.Context.CombatPower.Attacks.Single();
            Assert.Throws<InvalidOperationException>(() => s.ChooseBowAttack(0, 3));
            Assert.That(s.Context.CombatPower.Attacks.Single(), Is.SameAs(before));
            s.Advance(); Assert.Throws<InvalidOperationException>(() => s.ChooseBowAttack(0, 2));
        }
    }
}
