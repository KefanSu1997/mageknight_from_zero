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
    public sealed class PveMageUnitsTests
    {
        private string Folder => Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json");
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Folder);
        private PveBattleSession Create(bool fire, string enemy = "monster_032", int red = 2, int blue = 2)
        {
            var p = new PlayerState(); p.Units.Add(new UnitState(UnitJsonLoader.Load(Folder, fire ? "army_007" : "army_013")));
            p.Mana.AddCrystal(ManaColor.Red, red); p.Mana.AddCrystal(ManaColor.Blue, blue);
            foreach (int n in new[] { 16, 21, 0, 11 }) p.Deck.Hand.Add(new DeedCard("basic_card_" + n.ToString("000"), CardType.Action));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private void Play(PveBattleSession s, int n, string choice = "strong") => s.Play(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_" + n.ToString("000")), choice);
        private void PhysicalEight(PveBattleSession s)
        {
            Play(s, 21); Play(s, 11); s.PlaySideways(s.Player.Deck.Hand.Single(c => c.Id == "basic_card_000"));
        }
        private void End(PveBattleSession s, int fame)
        {
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Won, Is.True); Assert.That(s.Player.Fame, Is.EqualTo(fame)); Assert.That(s.Player.Wounds, Is.Zero);
            Assert.That(s.Player.Units.Single().IsReady, Is.False); Assert.That(s.Player.Units.Single().Wounds, Is.Zero);
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)] [TestCase(false, true)]
        public void ElementalMeleeAndBlockPayPrintedFeeAndResolveActualEnemy(bool fire, bool block)
        {
            var s = Create(fire, block ? fire ? "monster_004" : "monster_029" : "monster_006"); var u = s.Player.Units.Single();
            Assert.That(u.Card.Level, Is.EqualTo(3)); Assert.That(u.Card.InfluenceCost, Is.EqualTo(9));
            Assert.That(u.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.MageTower | RecruitLocation.Keep));
            s.Advance(); if (block) s.UseUnit(u, fire ? 1 : 0, 1); else Play(s, 16);
            Assert.That(s.BlockPreview(0).Effective, Is.EqualTo(block ? fire ? 6 : 4 : 5));
            s.Block(0); s.Advance(); s.Advance();
            if (block) PhysicalEight(s); else s.UseUnit(u, fire ? 1 : 0);
            var result = s.Attack(0); Assert.That(result.EffectivePower, Is.EqualTo(block ? 4 : fire ? 6 : 4));
            Assert.That(result.Kills, Is.EqualTo(1));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Red], Is.EqualTo(fire ? block ? 0 : 1 : block ? 1 : 2));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(1));
            End(s, block ? 6 : 4);
        }

        [TestCase(true)] [TestCase(false)]
        public void RangedFireAndSiegeIceKeepTypeAndElement(bool fire)
        {
            var s = Create(fire, fire ? "monster_032" : "monster_017"); var u = s.Player.Units.Single(); s.UseUnit(u, fire ? 0 : 1);
            var power = s.Context.CombatPower.Attacks.Single();
            Assert.That(power.Type, Is.EqualTo(fire ? AttackType.Ranged : AttackType.Siege));
            Assert.That(power.Element, Is.EqualTo(fire ? Element.Fire : Element.Ice));
            Assert.That(s.Player.Mana.Crystals[ManaColor.Blue], Is.EqualTo(fire ? 2 : 1));
            Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(fire ? 3 : 4)); End(s, fire ? 2 : 3);
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)] [TestCase(false, true)]
        public void GeneratedManaPaysActualCardAndCrystalOverflowIsConserved(bool fire, bool capped)
        {
            var s = Create(fire, red: fire ? capped ? 3 : 0 : 1, blue: fire ? 1 : capped ? 3 : 0);
            var p = s.Player; var u = p.Units.Single(); var color = fire ? ManaColor.Red : ManaColor.Blue;
            s.UseUnit(u, 2); Assert.That(p.Mana.Crystals[color], Is.EqualTo(capped ? 3 : 1));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(capped ? 2 : 1));
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2));
            Assert.That(p.Mana.Tokens[color], Is.EqualTo(capped ? 2 : 1));
            if (fire) { s.Advance(); Play(s, 16); s.Block(0); s.Advance(); s.Advance(); Play(s, 21); }
            else Play(s, 11);
            Assert.That(p.Mana.Tokens.GetValueOrDefault(color), Is.EqualTo(capped ? 1 : 0));
            Assert.That(p.Mana.Crystals[color], Is.EqualTo(capped ? 3 : 1));
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); End(s, fire ? 2 : 3);
        }

        [TestCase(true)] [TestCase(false)]
        public void ManaActivationDuringDamageExhaustsButResistanceStillAbsorbs(bool fire)
        {
            var s = Create(fire, fire ? "monster_029" : "monster_004"); var u = s.Player.Units.Single();
            Assert.That(u.Card.Abilities, Is.EquivalentTo(new[] { fire ? Ability.FireResist : Ability.IceResist }));
            s.Advance(); s.Advance(); s.UseUnit(u, 2); s.PlanDamage(0, 0, u); s.Advance();
            Assert.That(u.Wounds, Is.Zero); Assert.That(s.Player.Wounds, Is.Zero); Assert.That(u.IsReady, Is.False);
            Assert.That(s.Context.DamageAssignedUnits.Contains(u), Is.True);
            PhysicalEight(s); Assert.That(s.Attack(0).EffectivePower, Is.EqualTo(4)); End(s, 6);
        }

        [TestCase(true)] [TestCase(false)]
        public void MissingElementalFeePreservesReadinessAndResources(bool fire)
        {
            var s = Create(fire, red: 0, blue: 0); var u = s.Player.Units.Single();
            if (fire) s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 1, fire ? 1 : 0));
            Assert.That(u.IsReady, Is.True); Assert.That(s.Player.Mana.GetTotal(), Is.Zero);
            Assert.That(s.Context.CombatPower.Attacks, Is.Empty); Assert.That(s.Context.BlockPool, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void InvalidManaChoiceAndWoundedUnitAreRejectedBeforeResources(bool fire)
        {
            var s = Create(fire, red: 0, blue: 0); var u = s.Player.Units.Single();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2, 1));
            Assert.That(u.IsReady, Is.True); u.AddWounds(1);
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(u, 2));
            Assert.That(s.Player.Mana.GetTotal(), Is.Zero);
        }

        [Test] public void BattleOfferManaSpendsPermissionWithoutRecruitingOrMutatingSource()
        {
            var p = new PlayerState(); var card = UnitJsonLoader.Load(Folder, "army_007");
            var c = new ActionContext { UnitOffer = new List<UnitCard> { card } };
            new MK.Logic.Runtime.CardEffects.BattleSummonsEffect(false).Execute(p, c);
            CardUnitActions.UseFromOffer(p, c, 0, 2);
            Assert.That(p.Units, Is.Empty); Assert.That(c.UnitOffer.Single(), Is.SameAs(card)); Assert.That(c.OfferedUnitUses, Is.Zero);
            Assert.That(p.Mana.Crystals[ManaColor.Red], Is.EqualTo(1)); Assert.That(p.Mana.Tokens[ManaColor.Red], Is.EqualTo(1));
        }
    }
}
