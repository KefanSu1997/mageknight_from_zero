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
    public sealed class PveShockTroopsTests
    {
        [SetUp] public void Data() => CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(), "resources/text_json"));
        private PveBattleSession Create(string enemy = "monster_032", params string[] otherUnits)
        {
            var p = new PlayerState { Fame = 24 };
            p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_2_010")));
            foreach (var id in otherUnits) p.Units.Add(new UnitState(CardJsonLoader.LoadUnit(id)));
            return new PveBattleSession(p, CardJsonLoader.LoadMonsters().Where(m => m.Id == enemy), CardJsonLoader.LoadBasicActions());
        }
        private static void Play(PveBattleSession s, string id, string choice)
        {
            var card = new DeedCard(id, CardType.Action); s.Player.Deck.Hand.Add(card); s.Play(card, choice);
        }

        [TestCase(true)] [TestCase(false)]
        public void RangedBuffUsesActualOtherUnitContributionsBeforeOrAfterActivation(bool otherFirst)
        {
            var s = Create("monster_032", "army_2_012"); var shock = s.Player.Units[0]; var crossbow = s.Player.Units[1];
            Assert.That(shock.Card.Level, Is.EqualTo(2)); Assert.That(shock.Card.Armor, Is.EqualTo(3));
            Assert.That(shock.Card.InfluenceCost, Is.EqualTo(6)); Assert.That(shock.Card.RecruitLocationMask, Is.EqualTo(RecruitLocation.Keep));
            Assert.That(shock.Card.Actions.Length, Is.EqualTo(3)); Assert.That(shock.Card.Actions.SelectMany(a => a.Cost), Is.Empty);
            if (otherFirst) s.UseUnit(crossbow, 1);
            s.UseUnit(shock, 0);
            if (!otherFirst) s.UseUnit(crossbow, 1);
            Play(s, "basic_card_011", "base");
            Assert.That(s.Context.CombatPower.Attacks.Single(a => ReferenceEquals(a.SourceUnit, shock)).Value, Is.EqualTo(1));
            Assert.That(s.AttackTotal(AttackType.Ranged), Is.EqualTo(5), "奇袭1、弩手2+1、原卡1；原卡和自己不获单位加值");
            Assert.That(s.Attack(0).Kills, Is.EqualTo(1)); Assert.That(s.Context.CombatPower.Attacks, Is.Empty);
            Assert.That(shock.IsReady || crossbow.IsReady, Is.False);
            while (s.Phase != Phase.End) s.Advance();
            Assert.That(s.Context.OtherUnitAttackBonus(crossbow), Is.EqualTo(1), "原加值持续到回合末，不在战末消失");
            s.Context.SkipDraw = true; new PlayerTurnEngine().EndTurn(s.Player, DayPart.Day, null, s.Context);
            Assert.That(s.Context.OtherUnitAttackBonus(crossbow), Is.Zero); Assert.That(shock.IsReady, Is.False);
        }

        [Test]
        public void WeakenedArmorAndAttackFeedActualBlockAndMeleeConsumers()
        {
            var s = Create("monster_032", "army_2_000"); var shock = s.Player.Units[0];
            s.Advance(); s.UseUnit(shock, 1, target: 0);
            Assert.That(s.BlockPreview(0).Required, Is.EqualTo(3));
            Assert.That(CombatMath.EffectiveArmor(s.Enemies[0], 0, s.Context), Is.EqualTo(2));
            s.UseUnit(s.Player.Units[1], 1); s.Block(0); s.Advance(); s.Advance();
            Assert.That(s.Player.Wounds, Is.Zero);
            Play(s, "basic_card_021", "attack"); Assert.That(s.Attack(0).Kills, Is.EqualTo(1));
            s.Advance(); Assert.That(s.Won, Is.True); Assert.That(s.Context.UnitArmorReduction(s.Enemies[0]), Is.Zero);
        }

        [Test]
        public void SelectedAttackIsReducedBeforeSwiftMultiplication()
        {
            var s = Create("monster_007"); s.Advance(); s.UseUnit(s.Player.Units[0], 1, target: 0);
            Assert.That(s.BlockPreview(0).Required, Is.EqualTo(4));
        }

        [Test]
        public void MultiAttackChoiceLeavesTheOtherPrintedAttackUnchanged()
        {
            var s = Create("monster_058"); s.Advance(); s.UseUnit(s.Player.Units[0], 1, target: 0, attackIndex: 1);
            Assert.That(s.BlockPreview(0, 0).Required, Is.EqualTo(1)); Assert.That(s.BlockPreview(0, 1).Required, Is.Zero);
            s.Advance(); s.Advance(); Assert.That(s.Player.Wounds, Is.EqualTo(1));
        }

        [TestCase(1)] [TestCase(2)]
        public void ArcaneImmunityProtectsArmorButNotItsAttack(int ability)
        {
            var s = Create("monster_014"); s.Advance(); s.UseUnit(s.Player.Units[0], ability, target: 0);
            Assert.That(s.BlockPreview(0).Required, Is.EqualTo(7 - (ability == 1 ? 1 : 3)));
            Assert.That(CombatMath.EffectiveArmor(s.Enemies[0], 0, s.Context), Is.EqualTo(6));
        }

        [Test]
        public void InterceptionAutomaticallyAssignsDamageEvenWithoutManualPlan()
        {
            var s = Create(); var shock = s.Player.Units[0]; s.Advance(); s.UseUnit(shock, 2, target: 0);
            Assert.That(s.Context.ForcedDamageUnit(s.Enemies[0], 0), Is.SameAs(shock));
            Assert.That(s.BlockPreview(0).Required, Is.EqualTo(1));
            s.Advance(); s.PlanDamage(0, 0);
            Assert.That(s.DamagePlan.Single().Units.Single(), Is.SameAs(shock), "取消不能移除原规则的强制首位");
            s.Advance(); Assert.That(shock.Wounds, Is.EqualTo(1)); Assert.That(s.Player.Wounds, Is.Zero);
            s.Advance(); Assert.That(s.Context.ForcedDamageUnit(s.Enemies[0], 0), Is.Null);
        }

        [Test]
        public void BlockedOrZeroAttackDoesNotWoundInterceptingUnit()
        {
            var s = Create("monster_058"); var shock = s.Player.Units[0]; s.Advance(); s.UseUnit(shock, 2, target: 0, attackIndex: 1);
            s.Advance(); s.Advance(); Assert.That(shock.Wounds, Is.Zero); Assert.That(s.Player.Wounds, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPhaseAttackAndForbiddenUnitDamagePreserveReadyState()
        {
            var s = Create(); var shock = s.Player.Units[0];
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(shock, 1, target: 0)); s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(shock, 1, target: 0, attackIndex: 1));
            s.Context.NoUnitDamage = true;
            Assert.Throws<InvalidOperationException>(() => s.UseUnit(shock, 2, target: 0));
            Assert.That(shock.IsReady, Is.True); Assert.That(s.Context.PendingAttackReductions, Is.Empty);
            Assert.That(s.Context.ForcedDamageUnit(s.Enemies[0], 0), Is.Null);
        }

        // Contract test for the printed Assassination exception. The extra ability is an explicit
        // rules fixture, not a new original monster definition or a claimed scene/token verification.
        [TestCase(false)] [TestCase(true)]
        public void AssassinationAllowsOnlyBoundShockTroopsThenHero(bool intercept)
        {
            var s = Create("monster_014", "army_2_001");
            s.Enemies[0] = s.Enemies[0] with { Abilities = s.Enemies[0].Abilities.Concat(new[] { Ability.Assassination }).ToArray() };
            var shock = s.Player.Units[0]; var golem = s.Player.Units[1]; s.Advance();
            if (intercept) s.UseUnit(shock, 2, target: 0);
            s.Advance();
            Assert.Throws<InvalidOperationException>(() => s.PlanDamage(0, 0, golem));
            Assert.That(s.DamagePlan, Is.Empty); Assert.That(shock.Wounds + golem.Wounds + s.Player.Wounds, Is.Zero);
            s.Advance();
            Assert.That(shock.Wounds, Is.EqualTo(intercept ? 1 : 0)); Assert.That(golem.Wounds, Is.Zero);
            Assert.That(s.Player.Wounds, Is.EqualTo((int)Math.Ceiling((intercept ? 1.0 : 7.0) / s.Player.Armor)));
        }
    }
}
